using Azure.Data.Tables;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Tables;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ProfDropFunctions.Models;
using System.Net;
using System.Text.Json;

namespace ProfDropFunctions.Functions
{
    public class FeedbackFunctions
    {
        private readonly ILogger _logger;

        private const string FeedbackTableName = "Feedback";
        private const string CourseTableName = "Courses";
        private const string FeedbackPartition = "FEEDBACK";
        private const string CoursePartition = "COURSE";

        public FeedbackFunctions(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<FeedbackFunctions>();
        }

        // Submit anonymous student feedback
        [Function("CreateFeedback")]
        public async Task<HttpResponseData> CreateFeedback(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "feedback")] HttpRequestData req,
            [TableInput(FeedbackTableName, Connection = "AzureWebJobsStorage")] TableClient feedbackTableClient,
            [TableInput(CourseTableName, Connection = "AzureWebJobsStorage")] TableClient courseTableClient)
        {
            try
            {
                // Create the tables if they do not already exist
                await feedbackTableClient.CreateIfNotExistsAsync();
                await courseTableClient.CreateIfNotExistsAsync();

                var data = await JsonSerializer.DeserializeAsync<CreateFeedbackRequest>(
                    req.Body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (data == null)
                    return await BadRequest(req, "Invalid request body.");

                // Check that all required feedback information was provided
                if (string.IsNullOrWhiteSpace(data.CourseCode))
                    return await BadRequest(req, "Course code is required.");

                if (string.IsNullOrWhiteSpace(data.Category))
                    return await BadRequest(req, "Feedback category is required.");

                if (string.IsNullOrWhiteSpace(data.Message))
                    return await BadRequest(req, "Feedback message is required.");

                string courseCode = data.CourseCode.Trim().ToUpper();
                string category = data.Category.Trim();
                string message = data.Message.Trim();

                // Prevent extremely long feedback messages
                if (message.Length > 2000)
                    return await BadRequest(req, "Feedback message must be 2000 characters or less.");

                // Check that the category is one of the categories used by the system
                if (category.Equals("explanation", StringComparison.OrdinalIgnoreCase))
                    category = "Explanation";
                else if (category.Equals("lecture pace", StringComparison.OrdinalIgnoreCase))
                    category = "Lecture Pace";
                else if (category.Equals("course material", StringComparison.OrdinalIgnoreCase))
                    category = "Course Material";
                else if (category.Equals("assessment", StringComparison.OrdinalIgnoreCase))
                    category = "Assessment";
                else if (category.Equals("other", StringComparison.OrdinalIgnoreCase))
                    category = "Other";
                else
                    return await BadRequest(req, "Invalid feedback category. Use Lecture Pace, Explanation, Course Material, Assessment or Other.");

                // Check that the course exists before saving the feedback
                var courseResult = await courseTableClient.GetEntityIfExistsAsync<Course>(
                    CoursePartition,
                    courseCode);

                if (!courseResult.HasValue)
                    return await NotFound(req, "The course was not found.");

                // Create a unique ID for this anonymous feedback entry
                string feedbackId = Guid.NewGuid().ToString();

                var feedback = new Feedback
                {
                    PartitionKey = FeedbackPartition,
                    RowKey = feedbackId,
                    FeedbackId = feedbackId,
                    CourseCode = courseCode,
                    Category = category,
                    Message = message,
                    DateSubmitted = DateTimeOffset.UtcNow
                };

                await feedbackTableClient.AddEntityAsync(feedback);

                var response = req.CreateResponse(HttpStatusCode.Created);

                await response.WriteAsJsonAsync(new
                {
                    message = "Feedback submitted successfully.",
                    feedbackId = feedback.FeedbackId,
                    courseCode = feedback.CourseCode,
                    category = feedback.Category,
                    submittedAt = feedback.DateSubmitted
                });

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating feedback.");
                return await InternalServerError(req, "An error occurred while submitting feedback.");
            }
        }

        // Get feedback for a lecturer's course
        [Function("GetCourseFeedback")]
        public async Task<HttpResponseData> GetCourseFeedback(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "lecturers/{email}/courses/{code}/feedback")] HttpRequestData req,
            string email,
            string code,
            [TableInput(FeedbackTableName, Connection = "AzureWebJobsStorage")] TableClient feedbackTableClient,
            [TableInput(CourseTableName, Connection = "AzureWebJobsStorage")] TableClient courseTableClient)
        {
            try
            {
                // Create the tables if they do not already exist
                await feedbackTableClient.CreateIfNotExistsAsync();
                await courseTableClient.CreateIfNotExistsAsync();

                string lecturerId = email.Trim().ToLower();
                string courseCode = code.Trim().ToUpper();

                // Check that the course exists
                var courseResult = await courseTableClient.GetEntityIfExistsAsync<Course>(
                    CoursePartition,
                    courseCode);

                if (!courseResult.HasValue)
                    return await NotFound(req, "The course was not found.");

                var course = courseResult.Value;

                // Make sure the lecturer requesting the feedback owns this course
                if (!course.LecturerId.Equals(lecturerId, StringComparison.OrdinalIgnoreCase))
                    return await Forbidden(req, "You do not have access to this course's feedback.");

                string category = string.Empty;

                // Check if the lecturer selected a category filter
                var query = QueryHelpers.ParseQuery(req.Url.Query);

                if (query.TryGetValue("category", out var categoryValue))
                    category = categoryValue.ToString().Trim();

                // Validate and normalise the category filter
                if (!string.IsNullOrWhiteSpace(category))
                {
                    if (category.Equals("explanation", StringComparison.OrdinalIgnoreCase))
                        category = "Explanation";
                    else if (category.Equals("lecture pace", StringComparison.OrdinalIgnoreCase))
                        category = "Lecture Pace";
                    else if (category.Equals("course material", StringComparison.OrdinalIgnoreCase))
                        category = "Course Material";
                    else if (category.Equals("assessment", StringComparison.OrdinalIgnoreCase))
                        category = "Assessment";
                    else if (category.Equals("other", StringComparison.OrdinalIgnoreCase))
                        category = "Other";
                    else
                        return await BadRequest(req, "Invalid feedback category.");
                }

                var feedbackList = new List<Feedback>();

                // Get all feedback belonging to this course
                await foreach (Feedback feedback in feedbackTableClient.QueryAsync<Feedback>(x => x.PartitionKey == FeedbackPartition && x.CourseCode == courseCode))
                {
                    // If no category was selected, return all feedback.
                    // Otherwise, only return feedback matching the selected category.
                    if (string.IsNullOrWhiteSpace(category) || feedback.Category == category)
                        feedbackList.Add(feedback);
                }

                // Show the newest feedback first
                feedbackList = feedbackList.OrderByDescending(x => x.DateSubmitted).ToList();

                var response = req.CreateResponse(HttpStatusCode.OK);

                var feedbackResults = feedbackList.Select(feedback => new
                {
                    feedbackId = feedback.FeedbackId,
                    courseCode = feedback.CourseCode,
                    category = feedback.Category,
                    message = feedback.Message,
                    submittedAt = feedback.DateSubmitted
                }).ToList();

                await response.WriteAsJsonAsync(feedbackResults);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting course feedback.");
                return await InternalServerError(req, "An error occurred while getting course feedback.");
            }
        }

        // Returns HTTP 400 Bad Request when the feedback request data is invalid or missing
        private static async Task<HttpResponseData> BadRequest(HttpRequestData req, string message)
        {
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            await response.WriteAsJsonAsync(new { message });
            return response;
        }

        // Returns HTTP 404 Not Found when the requested course cannot be found
        private static async Task<HttpResponseData> NotFound(HttpRequestData req, string message)
        {
            var response = req.CreateResponse(HttpStatusCode.NotFound);
            await response.WriteAsJsonAsync(new { message });
            return response;
        }

        // Returns HTTP 403 Forbidden when a lecturer tries to access feedback for a course they do not own
        private static async Task<HttpResponseData> Forbidden(HttpRequestData req, string message)
        {
            var response = req.CreateResponse(HttpStatusCode.Forbidden);
            await response.WriteAsJsonAsync(new { message });
            return response;
        }

        // Returns HTTP 500 Internal Server Error when an unexpected error occurs
        private static async Task<HttpResponseData> InternalServerError(HttpRequestData req, string message)
        {
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteAsJsonAsync(new { message });
            return response;
        }
    }
}
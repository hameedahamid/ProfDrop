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
        // Logger used to record errors and information about feedback requests
        private readonly ILogger _logger;

        // Name of the Azure Table that stores anonymous student feedback
        private const string FeedbackTableName = "Feedback";
        // Name of the Azure Table that stores course information
        private const string CourseTableName = "Courses";
        // Partition key used for all feedback records
        private const string FeedbackPartition = "FEEDBACK";
        // Partition key used for all course records
        private const string CoursePartition = "COURSE";

        // Constructor used to create the logger for this class
        public FeedbackFunctions(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<FeedbackFunctions>();
        }

        // POST endpoint used by students to submit anonymous feedback
        [Function("CreateFeedback")]
        public async Task<HttpResponseData> CreateFeedback([HttpTrigger(AuthorizationLevel.Function, "post", Route = "feedback")] HttpRequestData req,
            [TableInput(FeedbackTableName, Connection = "AzureWebJobsStorage")] TableClient feedbackTableClient,
            [TableInput(CourseTableName, Connection = "AzureWebJobsStorage")] TableClient courseTableClient)
        {
            try
            {
                // Create the Feedback table if it does not already exist
                await feedbackTableClient.CreateIfNotExistsAsync();
                // Create the Courses table if it does not already exist
                await courseTableClient.CreateIfNotExistsAsync();

                // Read the JSON request body and convert it into a CreateFeedbackRequest object
                var data = await JsonSerializer.DeserializeAsync<CreateFeedbackRequest>(req.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                // Check that the request body was valid and could be read
                if (data == null)
                {
                    return await BadRequest(req, "Invalid request body.");
                }

                // Check that the student selected a course
                if (string.IsNullOrWhiteSpace(data.CourseCode))
                {
                    return await BadRequest(req, "Course code is required.");
                }

                // Check that the student selected a feedback category
                if (string.IsNullOrWhiteSpace(data.Category))
                {
                    return await BadRequest(req, "Feedback category is required.");
                }

                // Check that the student entered a feedback message
                if (string.IsNullOrWhiteSpace(data.Message))
                {


                    return await BadRequest(req, "Feedback message is required.");
                }


                // Clean the course code, category and feedback message
                string courseCode = data.CourseCode.Trim().ToUpper();
                string category = data.Category.Trim();
                string message = data.Message.Trim();

                // Prevent students from submitting feedback longer than 2000 characters
                if (message.Length > 2000)
                {

                    return await BadRequest(req, "Feedback message must be 2000 characters or less.");
                }

                // Convert the selected category to the standard category name used by the system
                if (category.Equals("explanation", StringComparison.OrdinalIgnoreCase))
                {
                    category = "Explanation";
                }
                else if (category.Equals("lecture pace", StringComparison.OrdinalIgnoreCase))
                {
                    category = "Lecture Pace";
                }
                else if (category.Equals("course material", StringComparison.OrdinalIgnoreCase))
                {
                    category = "Course Material";
                }
                else if (category.Equals("assessment", StringComparison.OrdinalIgnoreCase))
                {
                    category = "Assessment";
                }
                else if (category.Equals("other", StringComparison.OrdinalIgnoreCase))
                {
                    category = "Other";
                }
                else
                    return await BadRequest(req, "Invalid feedback category. Use Lecture Pace, Explanation, Course Material, Assessment or Other.");

                // Check that the selected course exists before saving the feedback
                var courseResult = await courseTableClient.GetEntityIfExistsAsync<Course>(CoursePartition, courseCode);

                // Return 404 if the selected course does not exist
                if (!courseResult.HasValue)
                {
                    return await NotFound(req, "The course was not found.");
                }

                // Generate a unique ID for this anonymous feedback entry
                string feedbackId = Guid.NewGuid().ToString();

                // Create the feedback record that will be saved to Azure Table Storage
                var feedback = new Feedback
                {
                    // Store all feedback records in the FEEDBACK partition
                    PartitionKey = FeedbackPartition,
                    // Use the generated ID as the unique RowKey
                    RowKey = feedbackId,
                    // Save the same ID in the FeedbackId property
                    FeedbackId = feedbackId,
                    // Save the course that the feedback belongs to
                    CourseCode = courseCode,
                    // Save the selected feedback category
                    Category = category,
                    // Save the student's anonymous feedback message
                    Message = message,
                    // Save the date and time that the feedback was submitted
                    DateSubmitted = DateTimeOffset.UtcNow
                };

                // Add the new feedback record to the Feedback table
                await feedbackTableClient.AddEntityAsync(feedback);

                // Create a 201 Created response because the feedback was successfully saved
                var response = req.CreateResponse(HttpStatusCode.Created);

                // Return the information needed to confirm that the feedback was submitted
                await response.WriteAsJsonAsync(new
                {
                    // Return a success message
                    message = "Feedback submitted successfully.",
                    // Return the generated feedback ID
                    feedbackId = feedback.FeedbackId,
                    // Return the course code that the feedback belongs to
                    courseCode = feedback.CourseCode,
                    // Return the feedback category that the student selected
                    category = feedback.Category,
                    // Return the date and time that the feedback was submitted
                    submittedAt = feedback.DateSubmitted
                });

                // Save the successful response back to the student 
                return response;
            }
            catch (Exception ex)
            {
                // Record the unexpected error in the function logs for debugging purposes
                _logger.LogError(ex, "Error creating feedback.");

                // Return a 500 Internal Server Error to the client 
                return await InternalServerError(req, "An error occurred while submitting feedback.");
            }
        }

        // GET endpoint used by a lecturer to view feedback for one of their courses
        [Function("GetCourseFeedback")]
        public async Task<HttpResponseData> GetCourseFeedback([HttpTrigger(AuthorizationLevel.Function, "get", Route = "lecturers/{email}/courses/{code}/feedback")] HttpRequestData req, string email, string code,
            [TableInput(FeedbackTableName, Connection = "AzureWebJobsStorage")] TableClient feedbackTableClient,
            [TableInput(CourseTableName, Connection = "AzureWebJobsStorage")] TableClient courseTableClient)
        {
            try
            {
                // Create the Feedback table if it does not already exist
                await feedbackTableClient.CreateIfNotExistsAsync();
                // Create the Courses table if it does not already exist
                await courseTableClient.CreateIfNotExistsAsync();

                // Clean the lecturer email and course code received in the URL
                string lecturerEmail = email.Trim().ToLower();
                string courseCode = code.Trim().ToUpper();

                // Check that the requested course exists
                var courseResult = await courseTableClient.GetEntityIfExistsAsync<Course>(CoursePartition, courseCode);

                // Return 404 if the requested course does not exist
                if (!courseResult.HasValue)
                {
                    return await NotFound(req, "The course was not found.");
                }

                // Get the course that was found 
                var course = courseResult.Value;

                // Make sure the lecturer requesting the feedback owns this course
                if (!course.LecturerEmail.Equals(lecturerEmail, StringComparison.OrdinalIgnoreCase))
                {
                    return await Forbidden(req, "You do not have access to this course's feedback.");
                }

                // Store the selected category filter
                string category = string.Empty;

                // Read the query string from the request
                var query = QueryHelpers.ParseQuery(req.Url.Query);

                // Check whether the lecturer selected a category filter in the query string
                if (query.TryGetValue("category", out var categoryValue))
                {
                    category = categoryValue.ToString().Trim();
                }

                // Validate and standardize the selected category
                if (!string.IsNullOrWhiteSpace(category))
                {
                    if (category.Equals("explanation", StringComparison.OrdinalIgnoreCase))
                    {
                        category = "Explanation";
                    }
                    else if (category.Equals("lecture pace", StringComparison.OrdinalIgnoreCase))
                    {
                        category = "Lecture Pace";
                    }
                    else if (category.Equals("course material", StringComparison.OrdinalIgnoreCase))
                    {
                        category = "Course Material";
                    }
                    else if (category.Equals("assessment", StringComparison.OrdinalIgnoreCase))
                    {
                        category = "Assessment";
                    }
                    else if (category.Equals("other", StringComparison.OrdinalIgnoreCase))
                    {
                        category = "Other";
                    }
                    else
                    {
                        return await BadRequest(req, "Invalid feedback category.");
                    }
                }

                // Create a list to hold the feedback that will be returned to the lecturer
                var feedbackList = new List<Feedback>();

                // Get feedback records from the Feedback table for this course 
                await foreach (Feedback feedback in feedbackTableClient.QueryAsync<Feedback>(x => x.PartitionKey == FeedbackPartition && x.CourseCode == courseCode))
                {
                    // If no category was selected, add all feedback for the course
                    // If a category was selected, only add feedback that matches the selected category
                    if (string.IsNullOrWhiteSpace(category) || feedback.Category == category)
                    {
                        feedbackList.Add(feedback);
                    }
                }

                // Sort the feedback so that the newest feedback appears first in the list returned to the lecturer
                feedbackList = feedbackList.OrderByDescending(x => x.DateSubmitted).ToList();

                // Create a 200 OK response because the feedback was successfully retrieved
                var response = req.CreateResponse(HttpStatusCode.OK);

                // Select only the feedback information that the MVC application needs
                var feedbackResults = feedbackList.Select(feedback => new
                {
                    // Return the unique feedback ID for each feedback entry
                    feedbackId = feedback.FeedbackId,
                    // Return the course code that the feedback belongs to
                    courseCode = feedback.CourseCode,
                    // Return the feedback category that the student selected
                    category = feedback.Category,
                    // Return the student's anonymous feedback message
                    message = feedback.Message,
                    // Return the date and time that the feedback was submitted
                    submittedAt = feedback.DateSubmitted
                }).ToList();

                // Send the feedback list back to the MVC application as JSON
                await response.WriteAsJsonAsync(feedbackResults);

                // Return the successful response
                return response;
            }
            catch (Exception ex)
            {
                // Record the unexpected error in the function logs for debugging purposes
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
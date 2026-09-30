using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Tables;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ProfDropFunctions.Models;
using System.Net;
using System.Text.Json;

namespace ProfDropFunctions.Functions
{
    public class CourseFunctions
    {
        private readonly ILogger _logger;

        private const string CourseTableName = "Courses";
        private const string LecturerTableName = "Lecturers";
        private const string CoursePartition = "COURSE";
        private const string LecturerPartition = "LECTURER";

        public CourseFunctions(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<CourseFunctions>();
        }

        // Create a new course
        [Function("CreateCourse")]
        public async Task<HttpResponseData> CreateCourse(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "courses")] HttpRequestData req,
            [TableInput(CourseTableName, Connection = "AzureWebJobsStorage")] TableClient courseTableClient,
            [TableInput(LecturerTableName, Connection = "AzureWebJobsStorage")] TableClient lecturerTableClient)
        {
            try
            {
                // Create the tables if they do not already exist
                await courseTableClient.CreateIfNotExistsAsync();
                await lecturerTableClient.CreateIfNotExistsAsync();

                var data = await JsonSerializer.DeserializeAsync<CreateCourseRequest>(
                    req.Body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (data == null)
                    return await BadRequest(req, "Invalid request body.");

                // Check that all required course information was provided
                if (string.IsNullOrWhiteSpace(data.CourseCode))
                    return await BadRequest(req, "Course code is required.");

                if (string.IsNullOrWhiteSpace(data.Name))
                    return await BadRequest(req, "Course name is required.");

                if (string.IsNullOrWhiteSpace(data.LecturerId))
                    return await BadRequest(req, "Lecturer ID is required.");

                string courseCode = data.CourseCode.Trim().ToUpper();
                string courseName = data.Name.Trim();
                // The Lecturer table uses the lecturer's email as the RowKey,
                // so LecturerId contains the lecturer's email in this version
                string lecturerId = data.LecturerId.Trim().ToLower();

                // Check that the lecturer exists using their email as the RowKey
                var lecturerResult = await lecturerTableClient.GetEntityIfExistsAsync<Lecturer>(
                    LecturerPartition,
                    lecturerId);

                if (!lecturerResult.HasValue)
                    return await NotFound(req, "The lecturer with this ID was not found.");

                // Check if the course already exists
                var existingCourse = await courseTableClient.GetEntityIfExistsAsync<Course>(
                    CoursePartition,
                    courseCode);

                if (existingCourse.HasValue)
                    return await Conflict(req, "A course with this course code already exists.");

                // Save the lecturer ID with the course so we know which lecturer owns it
                var course = new Course
                {
                    PartitionKey = CoursePartition,
                    RowKey = courseCode,
                    CourseCode = courseCode,
                    Name = courseName,
                    LecturerId = lecturerId
                };

                await courseTableClient.AddEntityAsync(course);

                var response = req.CreateResponse(HttpStatusCode.Created);

                await response.WriteAsJsonAsync(new
                {
                    message = "Course created successfully.",
                    courseCode = course.CourseCode,
                    courseName = course.Name,
                    lecturerId = course.LecturerId
                });

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating course.");
                return await InternalServerError(req, "An error occurred while creating the course.");
            }
        }

        // Get all courses for the student side
        [Function("GetCourses")]
        public async Task<HttpResponseData> GetCourses(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "courses")] HttpRequestData req,
            [TableInput(CourseTableName, Connection = "AzureWebJobsStorage")] TableClient courseTableClient,
            [TableInput(LecturerTableName, Connection = "AzureWebJobsStorage")] TableClient lecturerTableClient)
        {
            try
            {
                // Create the tables if they do not already exist
                await courseTableClient.CreateIfNotExistsAsync();
                await lecturerTableClient.CreateIfNotExistsAsync();

                var courses = new List<object>();

                // Get all courses from the COURSE partition
                await foreach (Course course in courseTableClient.QueryAsync<Course>(x => x.PartitionKey == CoursePartition))
                {
                    // Use the lecturer ID stored in the course to find the lecturer
                    var lecturerResult = await lecturerTableClient.GetEntityIfExistsAsync<Lecturer>(
                        LecturerPartition,
                        course.LecturerId);

                    if (lecturerResult.HasValue)
                    {
                        var lecturer = lecturerResult.Value;

                        courses.Add(new
                        {
                            courseCode = course.CourseCode,
                            courseName = course.Name,
                            lecturerId = course.LecturerId,
                            lecturerName = lecturer.Name,
                            profileUrl = lecturer.ProfileUrl
                        });
                    }
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(courses);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting courses.");
                return await InternalServerError(req, "An error occurred while getting courses.");
            }
        }

        // Get one course using the course code
        [Function("GetCourse")]
        public async Task<HttpResponseData> GetCourse(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "courses/{courseCode}")] HttpRequestData req,
            string courseCode,
            [TableInput(CourseTableName, Connection = "AzureWebJobsStorage")] TableClient courseTableClient,
            [TableInput(LecturerTableName, Connection = "AzureWebJobsStorage")] TableClient lecturerTableClient)
        {
            try
            {
                // Create the tables if they do not already exist
                await courseTableClient.CreateIfNotExistsAsync();
                await lecturerTableClient.CreateIfNotExistsAsync();

                courseCode = courseCode.Trim().ToUpper();

                // Find the course using the COURSE partition and course code as the RowKey
                var courseResult = await courseTableClient.GetEntityIfExistsAsync<Course>(
                    CoursePartition,
                    courseCode);

                if (!courseResult.HasValue)
                    return await NotFound(req, "Course was not found.");

                var course = courseResult.Value;

                // Use the lecturer ID stored in the course to find the lecturer
                var lecturerResult = await lecturerTableClient.GetEntityIfExistsAsync<Lecturer>(
                    LecturerPartition,
                    course.LecturerId);

                if (!lecturerResult.HasValue)
                    return await NotFound(req, "The lecturer assigned to this course was not found.");

                var lecturer = lecturerResult.Value;

                var response = req.CreateResponse(HttpStatusCode.OK);

                await response.WriteAsJsonAsync(new
                {
                    courseCode = course.CourseCode,
                    courseName = course.Name,
                    lecturerId = course.LecturerId,
                    lecturerName = lecturer.Name,
                    profileUrl = lecturer.ProfileUrl
                });

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting course.");
                return await InternalServerError(req, "An error occurred while getting the course.");
            }
        }

        // Get all courses belonging to a lecturer
        [Function("GetLecturerCourses")]
        public async Task<HttpResponseData> GetLecturerCourses(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "lecturers/{email}/courses")] HttpRequestData req,
            string email,
            [TableInput(CourseTableName, Connection = "AzureWebJobsStorage")] TableClient courseTableClient)
        {
            try
            {
                // Create the Courses table if it does not already exist
                await courseTableClient.CreateIfNotExistsAsync();

                string lecturerId = email.Trim().ToLower();

                var courses = new List<object>();

                // Find all courses where the lecturer ID matches
                await foreach (Course course in courseTableClient.QueryAsync<Course>(x => x.PartitionKey == CoursePartition && x.LecturerId == lecturerId))
                {
                    courses.Add(new
                    {
                        courseCode = course.CourseCode,
                        courseName = course.Name,
                        lecturerId = course.LecturerId
                    });
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(courses);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting lecturer courses.");
                return await InternalServerError(req, "An error occurred while getting lecturer courses.");
            }
        }

        // Returns HTTP 400 Bad Request when the request data is invalid or missing
        private static async Task<HttpResponseData> BadRequest(HttpRequestData req, string message)
        {
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            await response.WriteAsJsonAsync(new { message });
            return response;
        }

        // Returns HTTP 404 Not Found when the requested course or lecturer cannot be found
        private static async Task<HttpResponseData> NotFound(HttpRequestData req, string message)
        {
            var response = req.CreateResponse(HttpStatusCode.NotFound);
            await response.WriteAsJsonAsync(new { message });
            return response;
        }

        // Returns HTTP 409 Conflict when a course already exists
        private static async Task<HttpResponseData> Conflict(HttpRequestData req, string message)
        {
            var response = req.CreateResponse(HttpStatusCode.Conflict);
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
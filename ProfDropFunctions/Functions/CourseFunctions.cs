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
        // Logger used to record errors and information about course requests
        private readonly ILogger _logger;

        // Name of the Azure Table that stores course information
        private const string CourseTableName = "Courses";
        // Name of the Azure Table that stores lecturer information
        private const string LecturerTableName = "Lecturers";
        // Partition key used for all course records 
        private const string CoursePartition = "COURSE";
        // Partition key used for all lecturer records
        private const string LecturerPartition = "LECTURER";

        // Constructur used to create the logger for this class
        public CourseFunctions(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<CourseFunctions>();
        }

        // POST endpoint used to create a new course
        [Function("CreateCourse")]
        public async Task<HttpResponseData> CreateCourse(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "courses")] HttpRequestData req,
            [TableInput(CourseTableName, Connection = "AzureWebJobsStorage")] TableClient courseTableClient,
            [TableInput(LecturerTableName, Connection = "AzureWebJobsStorage")] TableClient lecturerTableClient)
        {
            try
            {
                // Create the Courses table if it does not already exist
                await courseTableClient.CreateIfNotExistsAsync();
                // Create the Lecturers table if it does not already exist
                await lecturerTableClient.CreateIfNotExistsAsync();

                // Read the JSON request body and convert it into a CreateCourseRequest object
                var data = await JsonSerializer.DeserializeAsync<CreateCourseRequest>(
                    req.Body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                // Check that the request body was valid 
                if (data == null)
                    return await BadRequest(req, "Invalid request body.");

                // Check that a course code was provided 
                if (string.IsNullOrWhiteSpace(data.CourseCode))
                    return await BadRequest(req, "Course code is required.");

                // Check that a course name was provided
                if (string.IsNullOrWhiteSpace(data.Name))
                    return await BadRequest(req, "Course name is required.");
                
                // Check that the lecturer email was provided
                if (string.IsNullOrWhiteSpace(data.LecturerEmail))
                    return await BadRequest(req, "Lecturer email is required.");

                // Clean the course code and convert it to uppercase 
                string courseCode = data.CourseCode.Trim().ToUpper();
                // Remove unnecessary spaces from the course name
                string courseName = data.Name.Trim();
                // The Lecturer table uses the lecturer's email as the RowKey,
                // Therefore, LecturerEmail contains the lecturer's email in this system
                string lecturerEmail = data.LecturerEmail.Trim().ToLower();

                // Check that the lecturer exists using their email as the RowKey
                var lecturerResult = await lecturerTableClient.GetEntityIfExistsAsync<Lecturer>(
                    LecturerPartition,
                    lecturerEmail);

                // Return a 404 Not Found response if the lecturer does not exist
                if (!lecturerResult.HasValue)
                    return await NotFound(req, "The lecturer with this email was not found.");

                // Check whether a course with this course code already exists 
                var existingCourse = await courseTableClient.GetEntityIfExistsAsync<Course>(
                    CoursePartition,
                    courseCode);

                // Do not allow two courses to use the same course code , return a 409 Conflict response if they do
                if (existingCourse.HasValue)
                    return await Conflict(req, "A course with this course code already exists.");

                // Create the new course record
                var course = new Course
                {
                    // Store all courses in the COURSE partition
                    PartitionKey = CoursePartition,
                    // Use the course code as the unique RowKey
                    RowKey = courseCode,
                    // Save the course code
                    CourseCode = courseCode,
                    // Save the course name
                    Name = courseName,
                    // Save the email of the lecturer assigned to this course
                    LecturerEmail = lecturerEmail
                };

                // Add the new course to Azure Table Storage
                await courseTableClient.AddEntityAsync(course);

                // Create a 201 Created response
                var response = req.CreateResponse(HttpStatusCode.Created);

                // Return the newly created course information
                await response.WriteAsJsonAsync(new
                {
                    // Return a success message
                    message = "Course created successfully.",
                    // Return the course code 
                    courseCode = course.CourseCode,
                    // Return the course name
                    courseName = course.Name,
                    // Return the lecturer email assigned to this course
                    lecturerEmail = course.LecturerEmail
                });

                // Send the response back to the client
                return response;
            }
            catch (Exception ex)
            {
                // Record the unexpected error in the Function logs
                _logger.LogError(ex, "Error creating course.");
                // Return a 500 Internal Server Error
                return await InternalServerError(req, "An error occurred while creating the course.");
            }
        }

        // GET endpoint used to display all courses on the student side
        [Function("GetCourses")]
        public async Task<HttpResponseData> GetCourses(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "courses")] HttpRequestData req,
            [TableInput(CourseTableName, Connection = "AzureWebJobsStorage")] TableClient courseTableClient,
            [TableInput(LecturerTableName, Connection = "AzureWebJobsStorage")] TableClient lecturerTableClient)
        {
            try
            {
                // Create the Courses table if it does not already exist
                await courseTableClient.CreateIfNotExistsAsync();
                // Create the Lecturers table if it does not already exist
                await lecturerTableClient.CreateIfNotExistsAsync();

                // Create a list to store the courses that will be returned 
                var courses = new List<object>();

                // Get all courses from the COURSE partition
                await foreach (Course course in courseTableClient.QueryAsync<Course>(x => x.PartitionKey == CoursePartition))
                {
                    // Use the lecturer email stored in the course to find the matching lecturer
                    var lecturerResult = await lecturerTableClient.GetEntityIfExistsAsync<Lecturer>(
                        LecturerPartition,
                        course.LecturerEmail);

                    // Only return the course if its lecturer still exists
                    if (lecturerResult.HasValue)
                    {
                        // Get the lecturer information
                        var lecturer = lecturerResult.Value;

                        // Add the course and lecturer information to the response list
                        courses.Add(new
                        {
                            // Return the course code
                            courseCode = course.CourseCode,
                            // Return the course name
                            courseName = course.Name,
                            // Return the lecturer email assigned to this course
                            lecturerEmail = course.LecturerEmail,
                            // Return the lecturer name assigned to this course
                            lecturerName = lecturer.Name,
                            // Return the lecturer profile image Url assigned to this course
                            profileUrl = lecturer.ProfileUrl
                        });
                    }
                }

                // Create a successful 200 OK response
                var response = req.CreateResponse(HttpStatusCode.OK);
                // Return the list of courses as JSON
                await response.WriteAsJsonAsync(courses);

                // Send the response back to the client
                return response;
            }
            catch (Exception ex)
            {
                // Record the unexpected error in the Function logs
                _logger.LogError(ex, "Error getting courses.");
                // Return a 500 Internal Server Error
                return await InternalServerError(req, "An error occurred while getting courses.");
            }
        }

        // GET endpoint used to get the details of one course using its course code
        [Function("GetCourse")]
        public async Task<HttpResponseData> GetCourse(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "courses/{courseCode}")] HttpRequestData req,
            string courseCode,
            [TableInput(CourseTableName, Connection = "AzureWebJobsStorage")] TableClient courseTableClient,
            [TableInput(LecturerTableName, Connection = "AzureWebJobsStorage")] TableClient lecturerTableClient)
        {
            try
            {
                // Create the Courses table if it does not already exist
                await courseTableClient.CreateIfNotExistsAsync();
                // Create the Lecturers table if it does not already exist
                await lecturerTableClient.CreateIfNotExistsAsync();

                // Clean the course code received in the URL and convert it to uppercase for consistency
                courseCode = courseCode.Trim().ToUpper();

                // Find the course using the COURSE partition and course code as the RowKey
                var courseResult = await courseTableClient.GetEntityIfExistsAsync<Course>(
                    CoursePartition,
                    courseCode);

                // Return a 404 Not Found response if the course does not exist
                if (!courseResult.HasValue)
                    return await NotFound(req, "Course was not found.");

                // Get the course that was found
                var course = courseResult.Value;

                // Use the lecturer email stored in the course to find the lecturer assigned to this course
                var lecturerResult = await lecturerTableClient.GetEntityIfExistsAsync<Lecturer>(
                    LecturerPartition,
                    course.LecturerEmail);

                // Return a 404 Not Found response if the lecturer assigned to this course cannot be found
                if (!lecturerResult.HasValue)
                    return await NotFound(req, "The lecturer assigned to this course was not found.");
                // Get the lecturer information  
                var lecturer = lecturerResult.Value;
                // Create a successful 200 OK response
                var response = req.CreateResponse(HttpStatusCode.OK);

                // Return the course and lecturer information as JSON
                await response.WriteAsJsonAsync(new
                {
                    // Return the course code
                    courseCode = course.CourseCode,
                    // Return the course name
                    courseName = course.Name,
                    // Return the lecturer email assigned to this course
                    lecturerEmail = course.LecturerEmail,
                    // Return the lecturer name assigned to this course
                    lecturerName = lecturer.Name,
                    // Return the lecturer profile image Url assigned to this course
                    profileUrl = lecturer.ProfileUrl
                });

                // Send the response back to the client
                return response;
            }
            catch (Exception ex)
            {
                // Record the unexpected error in the Function logs
                _logger.LogError(ex, "Error getting course.");
                // Return a 500 Internal Server Error
                return await InternalServerError(req, "An error occurred while getting the course.");
            }
        }

        // GET endpoint used to get all courses belonging to a specific lecturer
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

                // Clean the lecturer email received in the URL and convert it to lowercase for consistency
                string lecturerEmail = email.Trim().ToLower();

                // Create a list to store the lecturer's courses
                var courses = new List<object>();

                // Find all courses where the stored lecturer email matches the lecturer email from the request
                await foreach (Course course in courseTableClient.QueryAsync<Course>(x => x.PartitionKey == CoursePartition && x.LecturerEmail == lecturerEmail))
                {
                    // Add the lecturer's course to the response list
                    courses.Add(new
                    {
                        // Return the course code
                        courseCode = course.CourseCode,
                        // Return the course name
                        courseName = course.Name,
                        // Return the lecturer email assigned to this course
                        lecturerEmail = course.LecturerEmail
                    });
                }

                // Create a successful 200 OK response
                var response = req.CreateResponse(HttpStatusCode.OK);
                // Return the lecturer's courses as JSON
                await response.WriteAsJsonAsync(courses);
                // Send the response back to the client
                return response;
            }
            catch (Exception ex)
            {
                // Record the unexpected error in the Function logs
                _logger.LogError(ex, "Error getting lecturer courses.");
                // Return a 500 Internal Server Error
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
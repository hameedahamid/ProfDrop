using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Extensions.Tables;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using ProfDropFunctions.Models;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using MediaTypeHeaderValue = System.Net.Http.Headers.MediaTypeHeaderValue;
using ContentDispositionHeaderValue = System.Net.Http.Headers.ContentDispositionHeaderValue;

public class LecturerFunctions
{
    // Logger used to record errors and information.
    private readonly ILogger _logger;

    // Name of the Azure Table that stores lecturers.
    private const string TableName = "Lecturers";

    // Partition key used for all lecturer records.
    private const string LecturerPartition = "LECTURER";

    // Name of the Blob Storage container for lecturer images.
    private const string ImageContainer = "lecturer-images";

    public LecturerFunctions(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<LecturerFunctions>();
    }

    // POST endpoint used to create a new lecturer.
    [Function("CreateLecturer")]
    public async Task<HttpResponseData> CreateLecturer([HttpTrigger(AuthorizationLevel.Function, "post", Route = "lecturers")] HttpRequestData req,
        [TableInput(TableName, Connection = "AzureWebJobsStorage")] TableClient tableClient)
    {
        try
        {
            // Create the Lecturers table if it does not exist.
            await tableClient.CreateIfNotExistsAsync();

            // Check that the request contains form data.
            if (!req.Headers.TryGetValues("Content-Type", out var contentTypes))
            {
                return await BadRequest(req, "Content-Type must be multipart/form-data.");
            }

            // Get the Content-Type from the request.
            string contentType = contentTypes.First();

            if (!contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            {
                return await BadRequest(req, "Content-Type must be multipart/form-data.");
            }

            // Find the boundary that separates the form fields.
            var boundaryParameter = MediaTypeHeaderValue.Parse(contentType).Parameters.FirstOrDefault(p => p.Name.Equals("boundary", StringComparison.OrdinalIgnoreCase));

            // Check that a boundary was found.
            if (boundaryParameter == null)
            {
                return await BadRequest(req, "Multipart boundary is missing.");
            }

            // Remove extra quotation marks from the boundary.
            string? boundary = HeaderUtilities.RemoveQuotes(boundaryParameter.Value).Value;

            // Create a reader that will read each form field.
            var reader = new MultipartReader(boundary, req.Body);

            // Variables used to store the lecturer details.
            string name = string.Empty;
            string email = string.Empty;
            string password = string.Empty;

            // Variables used to store the image information.
            string imageExtension = string.Empty;
            string imageContentType = string.Empty;

            // Create temporary memory for the uploaded image.
            using var imageStream = new MemoryStream();

            // Read the first form section.
            var section = await reader.ReadNextSectionAsync();

            // Loop through every field in the form.
            while (section != null)
            {
                // Check that the section has Content-Disposition information.
                if (!string.IsNullOrWhiteSpace(section.ContentDisposition))
                {
                    // Read information about this form field.
                    var contentDisposition = ContentDispositionHeaderValue.Parse(section.ContentDisposition);

                    // Get the name of the form field.
                    string fieldName = HeaderUtilities.RemoveQuotes(contentDisposition.Name).Value ?? string.Empty;

                    // Get the file name if this section contains a file.
                    string? fileName = HeaderUtilities.RemoveQuotes(contentDisposition.FileName).Value;

                    // Check whether this section is the image.
                    if (fieldName == "image" && !string.IsNullOrWhiteSpace(fileName))
                    {
                        // Get the uploaded image extension.
                        imageExtension = Path.GetExtension(fileName).ToLower();

                        // Check that the image is JPG or PNG.
                        if (imageExtension != ".jpg" &&
                            imageExtension != ".jpeg" &&
                            imageExtension != ".png")
                        {
                            return await BadRequest(req, "Only JPG and PNG images are allowed.");
                        }

                        // Choose the correct image Content-Type.
                        imageContentType = imageExtension switch
                        {
                            ".jpg" => "image/jpeg",
                            ".jpeg" => "image/jpeg",
                            ".png" => "image/png",
                            _ => "application/octet-stream"
                        };

                        // Copy the uploaded image into temporary memory.
                        await section.Body.CopyToAsync(imageStream);

                        // Make sure the image is not empty.
                        if (imageStream.Length == 0)
                        {
                            return await BadRequest(req, "Please select an image.");
                        }
                    }

                    // Otherwise this is one of the text fields.
                    else
                    {
                        // Read the text value from the form field.
                        using var fieldReader = new StreamReader(section.Body);

                        // Get the value entered by the user.
                        string value = await fieldReader.ReadToEndAsync();

                        // Save the value in the correct variable.
                        switch (fieldName)
                        {
                            case "name":
                                name = value;
                                break;

                            case "email":
                                email = value;
                                break;

                            case "password":
                                password = value;
                                break;
                        }
                    }
                }
                // Move to the next form field.
                section = await reader.ReadNextSectionAsync();
            }

            // Check that the required information was provided and return 400 if information is missing.
            if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
            {
                return await BadRequest(req, "Name, email and password are required.");
            }

            // Check that an image was uploaded
            if (imageStream.Length == 0)
            {
                return await BadRequest(req, "A profile image is required.");
            }

            // Remove spaces 
            email = email.Trim().ToLower();
            name = name.Trim();

            // Check that the email has a valid format.
            if (!new EmailAddressAttribute().IsValid(email))
            {
                return await BadRequest(req, "Please enter a valid email address.");
            }

            // Validate the password using a regex - have at least 8 characters: 1 Uppercase, 1 Lowercase, 1 Number
            if (!Regex.IsMatch(password, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$"))
            {
                return await BadRequest(req, "Password must be at least 8 characters and contain an uppercase letter, lowercase letter and number.");
            }

            // Find the lecturer using the email as the RowKey.
            var existingLecturer = await tableClient.GetEntityIfExistsAsync<Lecturer>(LecturerPartition, email);

            // Do not allow the same email to be registered twice.
            if (existingLecturer.HasValue)
            {
                return await Conflict(req, "A lecturer with this email already exists.");
            }

            // Connect to the lecturer image Blob container.
            var containerClient =
                await GetLecturerImageContainerAsync();


            // Create a unique file name for the image.
            string blobName =
                $"{email}/{Guid.NewGuid()}{imageExtension}";


            // Get a reference to the new image in Blob Storage.
            var blobClient =
                containerClient.GetBlobClient(blobName);


            // Move the image stream back to the beginning.
            imageStream.Position = 0;


            // Upload the image to Blob Storage.
            await blobClient.UploadAsync(
                imageStream,
                new BlobUploadOptions
                {
                    // Save the correct image type.
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = imageContentType
                    }
                });

            // Create the new lecturer record.
            var lecturer = new Lecturer
            {
                // All lecturers use the same partition.
                PartitionKey = LecturerPartition,

                // Use the lecturer's email as their unique RowKey.
                RowKey = email,

                // Save their name.
                Name = name,

                // Save their email.
                Email = email,

                // Save the Blob Storage image URL.
                ProfileUrl = blobClient.Uri.ToString()
            };

            // Create the password hasher.
            var passwordHasher = new PasswordHasher<Lecturer>();

            // Hash the password instead of storing the normal password.
            lecturer.PasswordHash = passwordHasher.HashPassword(lecturer, password);

            // Add the lecturer to Azure Table Storage.
            await tableClient.AddEntityAsync(lecturer);

            // Create a 201 Created response.
            var response = req.CreateResponse(HttpStatusCode.Created);

            // Return the created lecturer's basic details.
            await response.WriteAsJsonAsync(new
            {
                // Return a success message.
                message = "Lecturer created successfully.",

                // Return the lecturer name.
                name = lecturer.Name,

                // Return the lecturer email.
                email = lecturer.Email,

                profileUrl = lecturer.ProfileUrl
            });

            // Send the response.
            return response;
        }
        // Catch any unexpected error.
        catch (Exception ex)
        {
            // Record the error.
            _logger.LogError(ex, "Error creating lecturer.");

            // Return a simple server error.
            return await InternalServerError(req, "An error occurred while creating the lecturer.");
        }
    }

    // POST endpoint used to log a lecturer in.
    [Function("LoginLecturer")]
    public async Task<HttpResponseData> LoginLecturer([HttpTrigger(AuthorizationLevel.Function, "post", Route = "lecturers/login")] HttpRequestData req,
        [TableInput(TableName, Connection = "AzureWebJobsStorage")] TableClient tableClient)
    {
        try
        {
            // Create the table if it does not exist.
            await tableClient.CreateIfNotExistsAsync();

            // Read the email and password from the request.
            var data = await JsonSerializer.DeserializeAsync<LoginRequest>(req.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Check that both login fields were entered.
            if (data == null ||
                string.IsNullOrWhiteSpace(data.Email) ||
                string.IsNullOrWhiteSpace(data.Password))
            {
                return await BadRequest(req, "Email and password are required.");
            }

            // Clean the email and change it to lowercase.
            string email = data.Email.Trim().ToLower();

            // Find the lecturer using the email as the RowKey.
            var lecturerResult = await tableClient.GetEntityIfExistsAsync<Lecturer>(LecturerPartition, email);

            // Return 401 if the lecturer was not found.
            if (!lecturerResult.HasValue)
            {
                return await Unauthorized(req, "Incorrect email or password.");
            }

            // Get the lecturer that was found.
            Lecturer lecturer = lecturerResult.Value;

            // Create the password hasher.
            var passwordHasher = new PasswordHasher<Lecturer>();

            // Compare the entered password to the saved password hash.
            var passwordResult = passwordHasher.VerifyHashedPassword(lecturer, lecturer.PasswordHash, data.Password);

            // Return 401 if the password is incorrect.
            if (passwordResult == PasswordVerificationResult.Failed)
            {
                return await Unauthorized(req, "Incorrect email or password.");
            }

            // Create a successful response.
            var response = req.CreateResponse(HttpStatusCode.OK);

            // Return the lecturer details needed by the MVC app.
            await response.WriteAsJsonAsync(new
            {
                message = "Login successful.",
                name = lecturer.Name,
                email = lecturer.Email,
                profileUrl = lecturer.ProfileUrl
            });

            // Send the response.
            return response;
        }
        // Catch unexpected errors.
        catch (Exception ex)
        {
            // Record the error.
            _logger.LogError(ex, "Error logging lecturer in.");

            // Return a server error.
            return await InternalServerError(req, "An error occurred while logging in.");
        }
    }

    // Helper method used to connect to the lecturer image container.
    private static async Task<BlobContainerClient> GetLecturerImageContainerAsync()
    {
        // Get the Azure Storage connection string.
        string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage") ?? throw new InvalidOperationException("Missing AzureWebJobsStorage setting.");

        // Connect to Azure Blob Storage.
        var blobServiceClient = new BlobServiceClient(connectionString);

        // Get the lecturer image container.
        var containerClient = blobServiceClient.GetBlobContainerClient(ImageContainer);

        // Create the container if it does not already exist.
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

        // Return the connected container.
        return containerClient;
    }

    // Helper method that returns 400 Bad Request.
    private static async Task<HttpResponseData> BadRequest(HttpRequestData req, string message)
    {
        var response = req.CreateResponse(HttpStatusCode.BadRequest);
        await response.WriteAsJsonAsync(new
        {
            error = message
        });
        return response;
    }

    // Helper method that returns 409 Conflict.
    private static async Task<HttpResponseData> Conflict(HttpRequestData req, string message)
    {
        var response = req.CreateResponse(HttpStatusCode.Conflict);
        await response.WriteAsJsonAsync(new
        {
            error = message
        });
        return response;
    }


    // Helper method that returns 401 Unauthorized.
    private static async Task<HttpResponseData> Unauthorized(HttpRequestData req, string message)
    {
        var response = req.CreateResponse(HttpStatusCode.Unauthorized);
        await response.WriteAsJsonAsync(new
        {
            error = message
        });
        return response;
    }


    // Helper method that returns 500 Internal Server Error.
    private static async Task<HttpResponseData> InternalServerError(HttpRequestData req, string message)
    {
        var response = req.CreateResponse(HttpStatusCode.InternalServerError);
        await response.WriteAsJsonAsync(new
        {
            error = message
        });
        return response;
    }
}
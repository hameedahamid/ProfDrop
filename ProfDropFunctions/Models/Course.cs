using Azure;
using Azure.Data.Tables;

namespace ProfDropFunctions.Models
{
    public class Course : ITableEntity
    {
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;
        public DateTimeOffset? Timestamp { get; set; } = default!;
        public ETag ETag { get; set; } = default!;

        public string CourseCode { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string LecturerEmail { get; set; } = default!;
    }

    public class CreateCourseRequest
    {
        public string CourseCode { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string LecturerEmail { get; set; } = default!;
    }
}

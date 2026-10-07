using Azure;
using Azure.Data.Tables;

namespace ProfDropFunctions.Models
{
    public class Feedback : ITableEntity
    {
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; } = default!;

        public string FeedbackId { get; set; } = default!;
        public string CourseCode { get; set; } = default!;
        public string Category { get; set; } = default!;
        public string Message { get; set; } = default!;
        public DateTimeOffset DateSubmitted { get; set; }
    }

    public class CreateFeedbackRequest
    {
        public string CourseCode { get; set; } = default!;
        public string Category { get; set; } = default!;
        public string Message { get; set; } = default!;
    }
}
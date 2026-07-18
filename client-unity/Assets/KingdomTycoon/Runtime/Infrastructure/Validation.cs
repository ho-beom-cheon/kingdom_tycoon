using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace KingdomTycoon.Infrastructure
{
    public enum ValidationSeverity
    {
        Warning = 0,
        Error = 1
    }

    public sealed class ValidationIssue : IComparable<ValidationIssue>
    {
        public ValidationIssue(
            string code,
            ValidationSeverity severity,
            string source,
            string location,
            string message,
            string relatedKey = null)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Severity = severity;
            Source = source ?? string.Empty;
            Location = location ?? string.Empty;
            Message = message ?? string.Empty;
            RelatedKey = relatedKey;
        }

        public string Code { get; }

        public ValidationSeverity Severity { get; }

        public string Source { get; }

        public string Location { get; }

        public string Message { get; }

        public string RelatedKey { get; }

        public int CompareTo(ValidationIssue other)
        {
            if (other == null)
            {
                return 1;
            }

            int sourceComparison = string.Compare(Source, other.Source, StringComparison.Ordinal);
            if (sourceComparison != 0)
            {
                return sourceComparison;
            }

            int locationComparison = string.Compare(Location, other.Location, StringComparison.Ordinal);
            return locationComparison != 0
                ? locationComparison
                : string.Compare(Code, other.Code, StringComparison.Ordinal);
        }

        public override string ToString()
        {
            return $"{Severity}:{Code}:{Source}:{Location}:{Message}";
        }
    }

    public sealed class ValidationReport
    {
        private readonly List<ValidationIssue> issues = new();

        public IReadOnlyList<ValidationIssue> Issues
        {
            get
            {
                var sorted = issues.OrderBy(issue => issue).ToList();
                return new ReadOnlyCollection<ValidationIssue>(sorted);
            }
        }

        public bool IsValid => issues.All(issue => issue.Severity != ValidationSeverity.Error);

        public void Add(ValidationIssue issue)
        {
            issues.Add(issue ?? throw new ArgumentNullException(nameof(issue)));
        }

        public void AddError(string code, string source, string location, string message, string relatedKey = null)
        {
            Add(new ValidationIssue(code, ValidationSeverity.Error, source, location, message, relatedKey));
        }

        public void AddWarning(string code, string source, string location, string message, string relatedKey = null)
        {
            Add(new ValidationIssue(code, ValidationSeverity.Warning, source, location, message, relatedKey));
        }

        public void Merge(ValidationReport other)
        {
            if (other == null)
            {
                return;
            }

            issues.AddRange(other.issues);
        }
    }
}

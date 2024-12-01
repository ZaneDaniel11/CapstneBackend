using System;

namespace Events.Models
{
    public class Event
    {
        public int EventID { get; set; }             // Primary key
        public string Subject { get; set; }          // Title of the event
        public string Description { get; set; }      // Description of the event
        public DateTime StartTime { get; set; }      // Event start time
        public DateTime EndTime { get; set; }        // Event end time
        public bool IsAllDay { get; set; }           // Indicates if the event is an all-day event
        public string RecurrenceRule { get; set; }   // Recurrence rule (optional)
        public string Location { get; set; }         // Location of the event
        public string Organizer { get; set; }        // Organizer of the event (optional)
        public string Color { get; set; }            // Color for the event (optional)
        public DateTime CreatedAt { get; set; }      // Creation timestamp
        public DateTime UpdatedAt { get; set; }      // Last updated timestamp
    }
}

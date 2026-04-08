using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.Core.Models;

namespace ImmichFrame.WebApi.Helpers
{
    public static class CalendarExtensionMethods
    {
        public static IAppointment ToAppointment(this Occurrence occurrence, TimeZoneInfo targetTimeZone)
        {
            if (occurrence.Source is CalendarEvent calendarEvent)
            {
                return calendarEvent.ToAppointment(targetTimeZone);
            }

            return new Appointment
            {
                StartTime = ConvertToTimeZone(occurrence.Period.StartTime, targetTimeZone),
                Duration = occurrence.Period.Duration,
                EndTime = ConvertToTimeZone(occurrence.Period.EndTime, targetTimeZone),
                Location = string.Empty
            };
        }

        public static IAppointment ToAppointment(this CalendarEvent calEvent, TimeZoneInfo targetTimeZone)
        {
            return new Appointment
            {
                Summary = calEvent.Summary,
                Description = calEvent.Description,
                StartTime = ConvertToTimeZone(calEvent.Start, targetTimeZone),
                Duration = calEvent.Duration,
                EndTime = ConvertToTimeZone(calEvent.End, targetTimeZone),
                Location = calEvent.Location
            };
        }

        private static DateTimeOffset ConvertToTimeZone(IDateTime dateTime, TimeZoneInfo targetTimeZone)
        {
            var utcDateTime = dateTime.AsUtc;
            return TimeZoneInfo.ConvertTime(
                new DateTimeOffset(DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc)),
                targetTimeZone);
        }
    }
}

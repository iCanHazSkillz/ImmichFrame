namespace ImmichFrame.Core.Interfaces
{
    public interface IClientSettings
    {
        public int Interval { get; }
        public double TransitionDuration { get; }
        public bool DownloadImages { get; }
        public int RenewImagesDuration { get; }
        public bool ShowClock { get; }
        public bool ShowWeather { get; }
        public bool ShowCalendar { get; }
        public bool ShowMetadata { get; }
        public string? ClockFormat { get; }
        public string? ClockDateFormat { get; }
        public bool ShowPhotoDate { get; }
        public bool ShowProgressBar { get; }
        public string? PhotoDateFormat { get; }
        public bool ShowPhotoTimeAgo { get; }
        public bool ShowImageDesc { get; }
        public bool ShowPeopleDesc { get; }
        public bool ShowPeopleAge { get; }
        public bool ShowTagsDesc { get; }
        public bool ShowAlbumName { get; }
        public bool ShowImageLocation { get; }
        public string? ImageLocationFormat { get; }
        public string? PrimaryColor { get; }
        public string? SecondaryColor { get; }
        public string Style { get; }
        public string? BaseFontSize { get; }
        public string? ClockFontSize { get; }
        public string? WeatherFontSize { get; }
        public string? CalendarFontSize { get; }
        public string? MetadataFontSize { get; }
        public string? CalendarTimeZone { get; }
        public string? CalendarDateFormat { get; }
        public int CalendarLookaheadDays { get; }
        public int CalendarMaxEvents { get; }
        public string CalendarSortDirection { get; }
        public string? ClockStyle { get; }
        public string? WeatherStyle { get; }
        public string? CalendarStyle { get; }
        public string? MetadataStyle { get; }
        public string ClockPosition { get; }
        public string WeatherPosition { get; }
        public string CalendarPosition { get; }
        public string MetadataPosition { get; }
        public List<string> WidgetStackOrder { get; }
        public bool ShowWeatherLocation { get; }
        public bool ShowWeatherDescription { get; }
        public string? WeatherIconUrl { get; }
        public bool ImageZoom { get; }
        public bool ImagePan { get; }
        public bool ImageFill { get; }
        public bool PlayAudio { get; }
        public string Layout { get; }
        public string Language { get; }
    }
}

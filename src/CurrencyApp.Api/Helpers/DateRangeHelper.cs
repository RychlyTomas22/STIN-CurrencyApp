namespace CurrencyApp.Api.Helpers
{
    public static class DateRangeHelper
    {
        public static List<DateOnly> GetDatesInclusive(DateOnly startDate, DateOnly endDate)
        {
            if (endDate < startDate)
            {
                throw new ArgumentException("End date must be greater than or equal to start date.");
            }

            var dates = new List<DateOnly>();
            var current = startDate;

            while (current <= endDate)
            {
                dates.Add(current);
                current = current.AddDays(1);
            }

            return dates;
        }
    }
}
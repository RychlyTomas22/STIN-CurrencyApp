using CurrencyApp.Api.Helpers;
using Xunit;

namespace CurrencyApp.Tests
{
    public class DateRangeHelperTests
    {
        [Fact]
        public void GetDatesInclusive_ShouldReturnAllDatesIncludingBounds()
        {
            var startDate = new DateOnly(2025, 1, 1);
            var endDate = new DateOnly(2025, 1, 3);

            var result = DateRangeHelper.GetDatesInclusive(startDate, endDate);

            Assert.Equal(3, result.Count);
            Assert.Equal(new DateOnly(2025, 1, 1), result[0]);
            Assert.Equal(new DateOnly(2025, 1, 2), result[1]);
            Assert.Equal(new DateOnly(2025, 1, 3), result[2]);
        }

        [Fact]
        public void GetDatesInclusive_ShouldReturnSingleDate_WhenStartEqualsEnd()
        {
            var startDate = new DateOnly(2025, 1, 1);
            var endDate = new DateOnly(2025, 1, 1);

            var result = DateRangeHelper.GetDatesInclusive(startDate, endDate);

            Assert.Single(result);
            Assert.Equal(new DateOnly(2025, 1, 1), result[0]);
        }

        [Fact]
        public void GetDatesInclusive_ShouldThrow_WhenEndDateIsBeforeStartDate()
        {
            var startDate = new DateOnly(2025, 1, 3);
            var endDate = new DateOnly(2025, 1, 1);

            var ex = Assert.Throws<ArgumentException>(() =>
                DateRangeHelper.GetDatesInclusive(startDate, endDate));

            Assert.Contains("End date must be greater than or equal to start date", ex.Message);
        }
    }
}
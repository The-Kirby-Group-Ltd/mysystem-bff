using mysystem_bff.Models;

namespace mysystem_bff.Services.Dashboard;

public static class DashboardDateHelper
{
    // =========================================================
    // YEAR VALIDATION
    // =========================================================

    public static bool IsValidYear(
        int year)
    {
        return
            year >= 2000 &&
            year <= DateTime.UtcNow.Year + 1;
    }

    // =========================================================
    // DATE RANGE
    // =========================================================

    public static (
        DateTime From,
        DateTime To)
        GetDateRange(
            MonthType month,
            int year)
    {
        if (month == MonthType.ALL)
        {
            var start =
                new DateTime(
                    year,
                    1,
                    1);

            var end =
                start
                    .AddYears(1)
                    .AddTicks(-1);

            return (
                start,
                end);
        }

        var monthNumber =
            month switch
            {
                MonthType.JAN => 1,
                MonthType.FEB => 2,
                MonthType.MAR => 3,
                MonthType.APR => 4,
                MonthType.MAY => 5,
                MonthType.JUN => 6,
                MonthType.JUL => 7,
                MonthType.AUG => 8,
                MonthType.SEP => 9,
                MonthType.OCT => 10,
                MonthType.NOV => 11,
                MonthType.DEC => 12,

                _ =>
                    throw new ArgumentOutOfRangeException(
                        nameof(month))
            };

        var startOfMonth =
            new DateTime(
                year,
                monthNumber,
                1);

        var endOfMonth =
            startOfMonth
                .AddMonths(1)
                .AddTicks(-1);

        return (
            startOfMonth,
            endOfMonth);
    }
}
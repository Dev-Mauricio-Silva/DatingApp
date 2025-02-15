namespace API.Extensions;

public static class DateTimeExtensions
{
    public static int CalculateAge(this DateOnly dateOfBirty)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        var age = today.Year - dateOfBirty.Year;

        if(dateOfBirty > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }
}

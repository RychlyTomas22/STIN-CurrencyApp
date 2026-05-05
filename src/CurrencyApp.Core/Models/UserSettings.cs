namespace CurrencyApp.Core.Models
{
    public class UserSettings
    {
        public string BaseCurrency { get; set; } = "USD";
        public List<string> SelectedCurrencies { get; set; } = new();
    }
}
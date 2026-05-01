using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CurrencyApp.Core.Models
{
    public class ExchangeRateSnapshot
    {
        public string BaseCurrency { get; set; } = string.Empty;
        public DateOnly Date { get; set; }
        public List<ExchangeRateValue> Rates { get; set; } = new();
    }
}

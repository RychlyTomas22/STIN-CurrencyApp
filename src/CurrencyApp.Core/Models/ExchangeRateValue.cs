using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CurrencyApp.Core.Models
{
    public class ExchangeRateValue
    {
        public string Currency { get; set; } = string.Empty;
        public decimal Rate { get; set; }
    }
}

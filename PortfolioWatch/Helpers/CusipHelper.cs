using System;
using System.Linq;

namespace PortfolioWatch.Helpers
{
    public static class CusipHelper
    {
        public static bool IsValidCusip(string input)
        {
            if (string.IsNullOrWhiteSpace(input) || input.Length != 9)
                return false;

            input = input.ToUpperInvariant();

            // Validate first 8 characters are alphanumeric
            string body = input.Substring(0, 8);
            if (!body.All(c => char.IsLetterOrDigit(c)))
                return false;

            // Validate 9th character is a digit
            if (!char.IsDigit(input[8]))
                return false;

            int sum = 0;

            for (int i = 0; i < 8; i++)
            {
                char c = body[i];
                int val;

                if (char.IsDigit(c))
                {
                    val = c - '0';
                }
                else
                {
                    val = c - 'A' + 10; // A=10, B=11, ...
                }

                // 1-based index: i+1
                // Even positions are doubled
                if ((i + 1) % 2 == 0)
                {
                    val *= 2;
                }

                // If >= 10, sum digits (e.g. 12 -> 1+2=3)
                // Shortcut: val / 10 + val % 10
                if (val >= 10)
                {
                    val = (val / 10) + (val % 10);
                }

                sum += val;
            }

            int mod = sum % 10;
            int checkDigit = (10 - mod) % 10;

            int actualCheckDigit = input[8] - '0';

            return checkDigit == actualCheckDigit;
        }
    }
}

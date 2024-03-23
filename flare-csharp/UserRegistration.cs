using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Zxcvbn;

namespace Flare
{
    public enum PasswordStrength
    {
        None,
        Unacceptable,
        Weak,
        Good,
        Excellent
    }

    public enum UsernameValidity
    {
        IsBlank,
        NotAllAscii,
        NotAllAlphanumerical,
        Correct
    }

    public static class UserRegistration
    {

        /// <summary>
        /// Evaluates password using zxcvbn algorithm <see href="https://github.com/trichards57/zxcvbn-cs" />
        /// </summary>
        /// <param name="password"></param>
        /// <returns>An evaluation of the given string as a password</returns>
        public static PasswordStrength EvaluatePassword(string password)
        {
            // just invalid input
            if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(password))
                return PasswordStrength.None;

            // Entropy is estimated using Dan Wheeler's zxcvbn algorithm.
            int entropy = (int)Math.Log2(
                Core.EvaluatePassword(password).Guesses
                );

            // [0;50] - unacceptable
            if (entropy <= 50)
                return PasswordStrength.Unacceptable;

            // (50; 70] - weak
            if (entropy <= 70)
                return PasswordStrength.Weak;

            // (70, 90] - good
            if (entropy <= 90)
                return PasswordStrength.Good;

            return PasswordStrength.Excellent;
        }

        /// <summary>
        /// Username by the protocol must contain only ASCII characters, can't be blank or empty and can only contain alphanumerical symbols (_ symbol included).
        /// </summary>
        /// <param name="username">String to check the validity as a username</param>
        /// <returns>Correct if all requirements are met</returns>
        public static UsernameValidity ValidifyUsername(string username)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrWhiteSpace(username))
                return UsernameValidity.IsBlank;

            if (!ContainsOnlyAscii(username))
                return UsernameValidity.NotAllAscii;

            Regex regex = new Regex(@"^[\d\w]{1,32}$", RegexOptions.IgnoreCase);
            if (!regex.IsMatch(username))
                return UsernameValidity.NotAllAlphanumerical;

            return UsernameValidity.Correct;
        }

        private static bool ContainsOnlyAscii(string str)
        {
            foreach (char c in str)
                if (!char.IsAscii(c))
                    return false;
            return true;
        }
    }
}

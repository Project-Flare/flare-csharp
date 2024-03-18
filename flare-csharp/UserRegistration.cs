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

    public class UserRegistration
    {
        public string Username
        {
            get => _username;

            set
            {
                if (ValidifyUsername(value) == UsernameValidity.Correct)
                    _username = value;
            }
        }
        public string Password
        {
            get => _password;

            set
            {
                var evaluation = EvaluatePassword(value);
                switch (evaluation)
                {
                    case PasswordStrength.None:
                        return;
                    case PasswordStrength.Unacceptable:
                        return;
                    case PasswordStrength.Weak:
                        return;
                    case PasswordStrength.Good:
                        _password = value;
                        return;
                    case PasswordStrength.Excellent:
                        _password = value;
                        return;
                    default:
                        return;
                }
            }
        }
        public bool IsValid
        {
            get
            {
                // Username or password are not set
                if (_username == string.Empty || _password == string.Empty)
                    return false;

                return true;
            }
        }
        public bool UsernameValid { get => _username != string.Empty; }
        public bool PasswordValid { get => _password != string.Empty; }

        private string _username;
        private string _password;

        public UserRegistration()
        {
            _username = string.Empty;
            _password = string.Empty;
        }

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

        public static bool ContainsOnlyAscii(string str)
        {
            foreach (char c in str)
                if (!char.IsAscii(c))
                    return false;
            return true;
        }

        public RegisterRequest? FormRegistrationRequest()
        {
            if (!UsernameValid || !PasswordValid)
                return null;

            RegisterRequest request = new RegisterRequest
            {
                Username = _username,
                Password = _password
            };

            return request;
        }
    }
}

using Octopus.TinyTypes;

namespace Sashimi.Server.Contracts.Accounts
{
    public class AccountType : CaseInsensitiveStringTinyType
    {
        public static readonly AccountType None = new AccountType(nameof (None));

        public AccountType(string value):base(value)
        {
        }
    }
}
using Prometeus.Core.Data.Context;
using Prometeus.Core.Data.Queries.DDL;
using Prometeus.Core.Data.Repositories;
using Prometeus.Core.Storage;
using Prometeus.Services.Configs;

namespace Prometeus.Services.Tests;

public class DepedencyFixture
{
    public DatabaseContext DatabaseContext { get; private set; }
    public KeyStore KeyStore { get; private set; }
    public static BankDetails BankDetails => new()
    {
        BIK = "045655635",
        Name = "Test bank",
        License = "3484",
        BINs = new()
        {
            ["Mir"] = ["220204", "220205", "220206"],
            ["Visa"] = ["440000", "450000"]
        },
        CurrencyCodes = new()
        {
            ["RUB"] = 643,
            ["USD"] = 840,
            ["EUR"] = 978
        }
    };
    public DepedencyFixture()
    {
        DatabaseContext = new DatabaseContext.Builder()
            .InMemory()
            .AddInitializationQuery(Tables.Clients)
            .AddInitializationQuery(Tables.Accounts)
            .AddInitializationQuery(Tables.Cards)
            .AddInitializationQuery(Tables.Transactions)
            .AddInitializationQuery(Triggers.AccountOnAfterClosed)
            .AddInitializationQuery(Indexes.Accounts)
            .Add(conn => new ClientRepository(conn))
            .Add(conn => new AccountRepository(conn))
            .Add(conn => new CardRepository(conn))
            .Add(conn => new TransactionRepository(conn))
            .Build();
        DatabaseContext.EnsureCreateAsync().GetAwaiter().GetResult();

        KeyStore = new KeyStore.Builder()
            .InMemory()
            .SetMasterKey("0123456789abcdef0123456789abcdef")
            .Build();
        KeyStore.EnsureCreate();
    }
}

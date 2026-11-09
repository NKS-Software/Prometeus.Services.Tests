using Prometeus.Core.Data.Entities;
using Prometeus.Core.Data.Repositories;
using Prometeus.Core.Generators;
using Prometeus.Core.Utils;
using Prometeus.Services.Models.DTOs;
using Prometeus.Services.Models.Requests;
using Prometeus.Services.Models.Responses;
using Prometeus.Services.Tests.Utils;

namespace Prometeus.Services.Tests;

public class CardServiceTests(DepedencyFixture depedencyFixture) : IClassFixture<DepedencyFixture>
{
    private readonly CardService cardService = new(
        depedencyFixture.DatabaseContext,
        DepedencyFixture.BankDetails,
        depedencyFixture.KeyStore);

    private readonly ClientRepository clientRepository = depedencyFixture.DatabaseContext.Get<ClientRepository>();
    private readonly AccountRepository accountRepository = depedencyFixture.DatabaseContext.Get<AccountRepository>();
    private readonly CardRepository cardRepository = depedencyFixture.DatabaseContext.Get<CardRepository>();

    #region GetBasicInfo
    [Fact]
    public async Task GetBasicInfo_WithAnotherIssuerBin()
    {
        var client = await CreateClientAsync();
        var account = await CreateAccountAsync(client.Id);
        // Генерируем карту с чужим BIN (например, 400000 - Visa, но не наш)
        var foreignCard = new Card
        {
            Number = CardNumberGenerator.Generate("400000"),
            AccountNumber = account.Number,
            CardHolderName = "TEST",
            ValidUntil = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1),
            PinHash = "hash",
            CvcKeyId = Guid.NewGuid()
        };
        await cardRepository.OpenAsync(foreignCard);

        var res = await cardService.GetBasicInfo(foreignCard.Number, client.Id);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task GetBasicInfo_WithNotExistsCard()
    {
        var client = await CreateClientAsync();
        var bin = DepedencyFixture.BankDetails.BINs.Values.First()[0];
        var number = CardNumberGenerator.Generate(bin);

        var res = await cardService.GetBasicInfo(number, client.Id);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task GetBasicInfo_WithAnotherClientCard()
    {
        var client1 = await CreateClientAsync();
        var account1 = await CreateAccountAsync(client1.Id);
        var card = await CreateCardAsync(account1.Number);

        var client2 = await CreateClientAsync();
        var res = await cardService.GetBasicInfo(card.Number, client2.Id);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task GetBasicInfo_WithValidData()
    {
        var client = await CreateClientAsync();
        var account = await CreateAccountAsync(client.Id);
        var card = await CreateCardAsync(account.Number);

        var res = await cardService.GetBasicInfo(card.Number, client.Id);
        Assert.True(res is SuccessResponse<CardDTO>);
        Assert.Equal(card.Number, ((SuccessResponse<CardDTO>)res).Value.Number);
    }
    #endregion

    #region GetFullInfo
    [Fact]
    public async Task GetFullInfo_WithValidData()
    {
        var client = await CreateClientAsync();
        var account = await CreateAccountAsync(client.Id);
        var card = await CreateCardAsync(account.Number);

        var res = await cardService.GetFullInfo(card.Number, client.Id);
        Assert.True(res is SuccessResponse<FullCardDto>);
        var dto = ((SuccessResponse<FullCardDto>)res).Value;
        Assert.Equal(card.Number, dto.Number);
        Assert.False(string.IsNullOrEmpty(dto.Cvc));
    }
    #endregion

    #region OpenAsync
    [Fact]
    public async Task OpenAsync_WithUnsupportedPaymentSystem()
    {
        var client = await CreateClientAsync();
        var account = await CreateAccountAsync(client.Id);
        var request = new OpenCardRequest
        {
            AccountNumber = account.Number,
            PaymentSystem = "Unknown",
            Pin = 1234,
            IsVirtual = false
        };

        var res = await cardService.OpenAsync(client.Id, request);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task OpenAsync_WithNotExistsAccount()
    {
        var client = await CreateClientAsync();
        var request = new OpenCardRequest
        {
            AccountNumber = AccountNumberGenerator.Generate(408, 02, 643, DepedencyFixture.BankDetails.BIK),
            PaymentSystem = "Visa",
            Pin = 1234,
            IsVirtual = false
        };

        var res = await cardService.OpenAsync(client.Id, request);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task OpenAsync_WithAnotherClientAccount()
    {
        var client1 = await CreateClientAsync();
        var account1 = await CreateAccountAsync(client1.Id);

        var client2 = await CreateClientAsync();
        var request = new OpenCardRequest
        {
            AccountNumber = account1.Number,
            PaymentSystem = "Visa",
            Pin = 1234,
            IsVirtual = false
        };

        var res = await cardService.OpenAsync(client2.Id, request);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task OpenAsync_WithValidData()
    {
        var client = await CreateClientAsync();
        var account = await CreateAccountAsync(client.Id);
        var request = new OpenCardRequest
        {
            AccountNumber = account.Number,
            PaymentSystem = "Mir",
            Pin = 1234,
            IsVirtual = false
        };

        var res = await cardService.OpenAsync(client.Id, request);
        var dto = ((SuccessResponse<FullCardDto>)res).Value;
        Assert.False(string.IsNullOrEmpty(dto.Cvc));
        Assert.False(string.IsNullOrEmpty(dto.Pin));
    }

    [Fact]
    public async Task OpenAsync_VirtualCard()
    {
        var client = await CreateClientAsync();
        var account = await CreateAccountAsync(client.Id);
        var request = new OpenCardRequest
        {
            AccountNumber = account.Number,
            PaymentSystem = "Mir",
            Pin = 0, // Виртуальная карта
            IsVirtual = true
        };

        var res = await cardService.OpenAsync(client.Id, request);
        Assert.True(res is SuccessResponse<FullCardDto>);
        Assert.Equal("Virtual", ((SuccessResponse<FullCardDto>)res).Value.CardHolderName);
    }
    #endregion

    #region ChangePinAsync
    [Fact]
    public async Task ChangePinAsync_WithValidData()
    {
        var client = await CreateClientAsync();
        var account = await CreateAccountAsync(client.Id);
        var card = await CreateCardAsync(account.Number);

        var request = new ChangePinRequest
        {
            CardNumber = card.Number,
            NewPin = 4321
        };

        var res = await cardService.ChangePinAsync(client.Id, request);
        Assert.True(res is SuccessResponse);

        // Проверяем, что хеш изменился
        var newHash = await cardRepository.GetPinHashAsync(card.Number);
        Assert.NotNull(newHash);
        Assert.NotEqual(card.PinHash, newHash);
    }
    #endregion

    #region GetAllForAccountAsync
    [Fact]
    public async Task GetAllForAccountAsync_WithAnotherClientAccount()
    {
        var client1 = await CreateClientAsync();
        var account1 = await CreateAccountAsync(client1.Id);

        var client2 = await CreateClientAsync();
        var res = await cardService.GetAllForAccountAsync(client2.Id, account1.Number);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task GetAllForAccountAsync_WithValidData()
    {
        var client = await CreateClientAsync();
        var account = await CreateAccountAsync(client.Id);
        await CreateCardAsync(account.Number);
        await CreateCardAsync(account.Number);

        var res = await cardService.GetAllForAccountAsync(client.Id, account.Number);
        Assert.True(res is SuccessResponse<List<CardDTO>>);
        Assert.Equal(2, ((SuccessResponse<List<CardDTO>>)res).Value.Count);
    }
    #endregion

    #region Freeze / Unfreeze / Lock
    [Fact]
    public async Task FreezeAsync_WithValidData()
    {
        var client = await CreateClientAsync();
        var account = await CreateAccountAsync(client.Id);
        var card = await CreateCardAsync(account.Number);

        var res = await cardService.FreezeAsync(client.Id, card.Number);
        Assert.True(res is SuccessResponse);

        var updated = await cardRepository.GetByNumberAsync(card.Number);
        Assert.Equal(CardStatus.Frozen, updated!.Status);
    }

    [Fact]
    public async Task UnfreezeAsync_WithValidData()
    {
        var client = await CreateClientAsync();
        var account = await CreateAccountAsync(client.Id);
        var card = await CreateCardAsync(account.Number);
        await cardService.FreezeAsync(client.Id, card.Number);

        var res = await cardService.UnfreezeAsync(client.Id, card.Number);
        if (res is ErrorResponse error)
        {
            Assert.Fail($"UnfreezeAsync failed: {error.Message} (Code: {error.ErrorCode})");
        }

        var updated = await cardRepository.GetByNumberAsync(card.Number);
        Assert.Equal(CardStatus.Active, updated!.Status);
    }

    [Fact]
    public async Task LockAsync_WithValidData()
    {
        var client = await CreateClientAsync();
        var account = await CreateAccountAsync(client.Id);
        var card = await CreateCardAsync(account.Number);

        var res = await cardService.LockAsync(client.Id, card.Number);
        Assert.True(res is SuccessResponse);

        var updated = await cardRepository.GetByNumberAsync(card.Number);
        Assert.Equal(CardStatus.Locked, updated!.Status);
    }
    #endregion

    #region Utils
    private async Task<Client> CreateClientAsync()
    {
        var client = DataGenerators.CreateRandomClient();
        var added = await clientRepository.AddAsync(client);
        Assert.NotNull(added);
        return added;
    }

    private async Task<Account> CreateAccountAsync(Guid clientId)
    {
        var account = DataGenerators.CreateRandomAccount(clientId);
        var opened = await accountRepository.OpenAsync(account);
        Assert.NotNull(opened);
        return opened;
    }

    private async Task<Card> CreateCardAsync(string accountNumber)
    {
        var bin = DepedencyFixture.BankDetails.BINs.Values.First()[0];

        var cvc = "123"; // Тестовый CVC
        var cvcKeyId = await depedencyFixture.KeyStore.WriteAsync(cvc);

        var card = new Card
        {
            AccountNumber = accountNumber,
            Number = CardNumberGenerator.Generate(bin),
            CardHolderName = "TEST CLIENT",
            ValidUntil = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(3),
            PinHash = Hasher.Hash("1234"),
            CvcKeyId = cvcKeyId
        };
        var created = await cardRepository.OpenAsync(card);
        Assert.NotNull(created);
        return created;
    }
    #endregion
}
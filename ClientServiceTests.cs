using Prometeus.Core.Data.Entities;
using Prometeus.Core.Data.Repositories;
using Prometeus.Core.Models;
using Prometeus.Services.Models.DTOs;
using Prometeus.Services.Models.Requests;
using Prometeus.Services.Models.Responses;
using Prometeus.Services.Tests.Utils;

namespace Prometeus.Services.Tests;

public class ClientServiceTests(DepedencyFixture depedencyFixture) : IClassFixture<DepedencyFixture>
{
    private readonly ClientService clientService = new(depedencyFixture.DatabaseContext);
    private readonly ClientRepository clientRepository = depedencyFixture.DatabaseContext.Get<ClientRepository>();
    private readonly AccountRepository accountRepository = depedencyFixture.DatabaseContext.Get<AccountRepository>();

    #region GetByIdAsync
    [Fact]
    public async Task GetByIdAsync_WithNotExistsClient()
    {
        var res = await clientService.GetByIdAsync(Guid.NewGuid());
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task GetByIdAsync_WithExistsClient()
    {
        var client = await CreateClientAsync();
        var res = await clientService.GetByIdAsync(client.Id);
        Assert.True(res is SuccessResponse<ClientDTO>);
        Assert.Equal(client.Name, ((SuccessResponse<ClientDTO>)res).Value.Name);
    }
    #endregion

    #region AddAsync
    [Fact]
    public async Task AddAsync_WithValidData()
    {
        var request = new AddClientRequest
        {
            Name = "Nikita",
            Surname = "Prostov",
            BirthDate = DateOnly.Parse("10-03-2003"),
            Email = $"prostov{Random.Shared.Next(0, 100)}@gmail.com",
            Phone = $"+790615722{Random.Shared.Next(10, 99)}"
        };
        var res = await clientService.AddAsync(request);
        Assert.True(res is SuccessResponse<AddClientResponse>);
    }

    [Fact]
    public async Task AddAsync_WithDuplicatedEmail()
    {
        var request = new AddClientRequest
        {
            Name = "Nikita",
            Surname = "Prostov",
            BirthDate = DateOnly.Parse("10-03-2003"),
            Email = "prostovn@gmail.com",
            Phone = "+79061572216"
        };
        await clientService.AddAsync(request);

        request = new AddClientRequest
        {
            Name = "Nikita",
            Surname = "Prostov",
            BirthDate = DateOnly.Parse("10-03-2003"),
            Email = "prostovn@gmail.com",
            Phone = "+79061562233"
        };
        var res = await clientService.AddAsync(request);
        Assert.False(res is SuccessResponse<AddClientResponse>);
    }

    [Fact]
    public async Task AddAsync_WithDuplicatedPhone()
    {
        var request = new AddClientRequest
        {
            Name = "Nikita",
            Surname = "Prostov",
            BirthDate = DateOnly.Parse("10-03-2003"),
            Email = "prostovn@gmail.com",
            Phone = "+79061572216"
        };
        await clientService.AddAsync(request);

        request = new AddClientRequest
        {
            Name = "Nikita",
            Surname = "Prostov",
            BirthDate = DateOnly.Parse("10-03-2003"),
            Email = "prostov2003@gmail.com",
            Phone = "+79061572216"
        };
        var res = await clientService.AddAsync(request);
        Assert.False(res is SuccessResponse<AddClientResponse>);
    }
    #endregion

    #region UpdateAsync
    [Fact]
    public async Task UpdateAsync_WithNotExistsClient()
    {
        var request = new UpdateClientRequest
        {
            Name = "NewName",
            Surname = "NewSurname",
            Patronimyc = null,
            BirthDate = null
        };
        var res = await clientService.UpdateAsync(Guid.NewGuid(), request);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task UpdateAsync_WithValidData()
    {
        var client = await CreateClientAsync();
        var request = new UpdateClientRequest
        {
            Name = "NewName",
            Surname = "NewSurname",
            Patronimyc = null,
            BirthDate = null
        };
        var res = await clientService.UpdateAsync(client.Id, request);
        Assert.True(res is SuccessResponse<ClientDTO>);
        Assert.Equal("NewName", ((SuccessResponse<ClientDTO>)res).Value.Name);
    }

    [Fact]
    public async Task UpdateAsync_WithPartialData()
    {
        var client = await CreateClientAsync();
        var request = new UpdateClientRequest
        {
            Name = "NewName",
            Surname = null,
            Patronimyc = null,
            BirthDate = null
        };
        var res = await clientService.UpdateAsync(client.Id, request);
        Assert.True(res is SuccessResponse<ClientDTO>);
        Assert.Equal("NewName", ((SuccessResponse<ClientDTO>)res).Value.Name);
        Assert.Equal(client.Surname, ((SuccessResponse<ClientDTO>)res).Value.Surname);
    }
    #endregion

    #region DeleteAsync
    [Fact]
    public async Task DeleteAsync_WithNotExistsClient()
    {
        var res = await clientService.DeleteAsync(Guid.NewGuid());
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task DeleteAsync_WithActiveAccounts()
    {
        var client = await CreateClientAsync();
        var account = DataGenerators.CreateRandomAccount(client.Id);
        await accountRepository.OpenAsync(account);

        var res = await clientService.DeleteAsync(client.Id);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task DeleteAsync_WithValidData()
    {
        var client = await CreateClientAsync();
        var res = await clientService.DeleteAsync(client.Id);
        Assert.True(res is SuccessResponse);
    }

    [Fact]
    public async Task DeleteAsync_WithClosedAccounts()
    {
        var client = await CreateClientAsync();
        var account = DataGenerators.CreateRandomAccount(client.Id);
        var opened = await accountRepository.OpenAsync(account);
        Assert.NotNull(opened);
        await accountRepository.CloseAsync(opened.Number);

        var res = await clientService.DeleteAsync(client.Id);
        Assert.True(res is SuccessResponse);
    }
    #endregion

    #region ChangeEmailAsync
    [Fact]
    public async Task ChangeEmailAsync_WithNotExistsClient()
    {
        var res = await clientService.ChangeEmailAsync(Guid.NewGuid(), "newemail@gmail.com");
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task ChangeEmailAsync_WithDuplicatedEmail()
    {
        var client1 = await CreateClientAsync(email: "client1@gmail.com");
        var client2 = await CreateClientAsync(email: "client2@gmail.com");

        var res = await clientService.ChangeEmailAsync(client2.Id, "client1@gmail.com");
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task ChangeEmailAsync_WithValidData()
    {
        var client = await CreateClientAsync();
        var newEmail = $"new{Random.Shared.Next(0, 1000)}@gmail.com";
        var res = await clientService.ChangeEmailAsync(client.Id, newEmail);
        Assert.True(res is SuccessResponse<ClientDTO>);
        Assert.Equal(newEmail, ((SuccessResponse<ClientDTO>)res).Value.Email);
    }
    #endregion

    #region ChangePhoneAsync
    [Fact]
    public async Task ChangePhoneAsync_WithNotExistsClient()
    {
        var res = await clientService.ChangePhoneAsync(Guid.NewGuid(), "+79061570000");
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task ChangePhoneAsync_WithDuplicatedPhone()
    {
        var client1 = await CreateClientAsync(phone: "+79061571111");
        var client2 = await CreateClientAsync(phone: "+79061572222");

        var res = await clientService.ChangePhoneAsync(client2.Id, "+79061571111");
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task ChangePhoneAsync_WithValidData()
    {
        var client = await CreateClientAsync();
        var newPhone = $"+7906157{Random.Shared.Next(1000, 9999)}";
        var res = await clientService.ChangePhoneAsync(client.Id, newPhone);
        Assert.True(res is SuccessResponse<ClientDTO>);
        Assert.Equal(newPhone, ((SuccessResponse<ClientDTO>)res).Value.Phone);
    }
    #endregion

    #region Utils
    private async Task<Client> CreateClientAsync(string? email = null, string? phone = null)
    {
        var client = DataGenerators.CreateRandomClient();
        if (email != null) client.Email = email;
        if (phone != null) client.Phone = phone;
        var added = await clientRepository.AddAsync(client);
        Assert.NotNull(added);
        return added;
    }
    #endregion
}
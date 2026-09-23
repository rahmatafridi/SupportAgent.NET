using SupportAgent.Infrastructure.Services;

namespace SupportAgent.Tests;

public class CustomerServiceTests
{
    [Fact]
    public async Task GetCustomerAsync_ReturnsCustomer_WhenCustomerExists()
    {
        await using var context = TestDbContextFactory.CreateContext(nameof(GetCustomerAsync_ReturnsCustomer_WhenCustomerExists));
        await TestDbContextFactory.SeedSampleDataAsync(context);

        var service = new CustomerService(context);
        var customer = await service.GetCustomerAsync(101);

        Assert.NotNull(customer);
        Assert.Equal("John", customer.FirstName);
        Assert.Equal("Smith", customer.LastName);
    }

    [Fact]
    public async Task GetCustomerAsync_ReturnsNull_WhenCustomerDoesNotExist()
    {
        await using var context = TestDbContextFactory.CreateContext(nameof(GetCustomerAsync_ReturnsNull_WhenCustomerDoesNotExist));

        var service = new CustomerService(context);
        var customer = await service.GetCustomerAsync(999);

        Assert.Null(customer);
    }
}

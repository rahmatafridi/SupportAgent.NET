using SupportAgent.Infrastructure.Services;

namespace SupportAgent.Tests;

public class OrderServiceTests
{
    [Fact]
    public async Task GetOrdersByCustomerAsync_ReturnsOrdersForCustomer()
    {
        await using var context = TestDbContextFactory.CreateContext(nameof(GetOrdersByCustomerAsync_ReturnsOrdersForCustomer));
        await TestDbContextFactory.SeedSampleDataAsync(context);

        var service = new OrderService(context);
        var orders = await service.GetOrdersByCustomerAsync(101);

        Assert.Equal(2, orders.Count);
        Assert.All(orders, order => Assert.Equal(101, order.CustomerId));
    }

    [Fact]
    public async Task GetOrderAsync_ReturnsOrder_WhenOrderExists()
    {
        await using var context = TestDbContextFactory.CreateContext(nameof(GetOrderAsync_ReturnsOrder_WhenOrderExists));
        await TestDbContextFactory.SeedSampleDataAsync(context);

        var service = new OrderService(context);
        var order = await service.GetOrderAsync(1);

        Assert.NotNull(order);
        Assert.Equal("ORD-1001", order.OrderNumber);
    }
}

using SupportAgent.Infrastructure.Services;

namespace SupportAgent.Tests;

public class TicketServiceTests
{
    [Fact]
    public async Task GetTicketMessagesAsync_ReturnsMessagesInCreatedOrder()
    {
        await using var context = TestDbContextFactory.CreateContext(nameof(GetTicketMessagesAsync_ReturnsMessagesInCreatedOrder));
        await TestDbContextFactory.SeedSampleDataAsync(context);

        var service = new TicketService(context);
        var messages = await service.GetTicketMessagesAsync(1001);

        Assert.Equal(2, messages.Count);
        Assert.Equal("Customer", messages[0].SenderType);
        Assert.Equal("Agent", messages[1].SenderType);
    }

    [Fact]
    public async Task GetTicketAsync_ReturnsNull_WhenTicketDoesNotExist()
    {
        await using var context = TestDbContextFactory.CreateContext(nameof(GetTicketAsync_ReturnsNull_WhenTicketDoesNotExist));

        var service = new TicketService(context);
        var ticket = await service.GetTicketAsync(9999);

        Assert.Null(ticket);
    }
}


using FluentEmail.Core;
using Microsoft.EntityFrameworkCore;
using OutboxPattern.WebAPI.Context;

namespace OutboxPattern.WebAPI.BackgroundServices
{
    public sealed class OrderBackgroundService(IServiceProvider serviceProvider) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using (var scoped = serviceProvider.CreateScope())
            {
                var context = scoped.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var fluentEmail = scoped.ServiceProvider.GetRequiredService<IFluentEmail>();

                while (!stoppingToken.IsCancellationRequested)
                {
                    var outboxes = await context.OrderOutboxes.
                    Where(x => !x.IsCompleted).
                    OrderBy(p => p.CreateAt).
                    ToListAsync(stoppingToken);

                    foreach (var item in outboxes)
                    {
                        try
                        {
                            if (item.Attempt>=3)
                            {
                                item.IsCompleted = true;
                                item.CompleteDate=DateTimeOffset.Now;
                                item.IsFailed = true;
                                item.FailMessage = "Mail delivery failed";
                                continue;
                            }

                            var order = await context.Orders.FirstAsync(p => p.Id == item.OrderId, stoppingToken);

                            string body = @"
<h1>order status:<b>Success</b></h1>
<p>Product Name: {productname}</p>
<p>Your order has been received</p>
<p>An informational email will be sent regarding the process.</p>";

                            body = body.Replace("{productname}", order.ProductName);

                            var response = await fluentEmail.To(order.CustomerEmail).
                                Subject("Order created").
                                Body(body).
                                SendAsync(stoppingToken);

                            if (!response.Successful)
                            {
                                item.Attempt++;
                            }
                            else
                            {
                                item.IsCompleted = true;
                                item.CompleteDate = DateTimeOffset.Now;
                            }
                        }
                        catch (Exception exception)
                        {
                            item.Attempt++;
                            if (item.Attempt >= 3)
                            {
                                item.IsFailed = true;
                                item.IsCompleted = true;
                                item.CompleteDate = DateTimeOffset.Now;
                                item.FailMessage = exception.Message;
                            }
                        }

                        await Task.Delay(TimeSpan.FromSeconds(100));
                    }
                    await context.SaveChangesAsync(stoppingToken);

                    await Task.Delay(TimeSpan.FromMinutes(2));
                }
            }
        }
    }
}

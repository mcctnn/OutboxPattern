using Mapster;
using Microsoft.EntityFrameworkCore;
using OutboxPattern.WebAPI.BackgroundServices;
using OutboxPattern.WebAPI.Context;
using OutboxPattern.WebAPI.Dtos;
using OutboxPattern.WebAPI.Models;
using Scalar.AspNetCore;
using TS.Result;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCors();
builder.Services.AddDbContext<ApplicationDbContext>(opt =>
{
    opt.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer"));
});

builder.Services.AddFluentEmail("info@outbox.com").AddSmtpSender("localhost", 25);
builder.Services.AddHostedService<OrderBackgroundService>();

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference();
app.UseCors(x => x.AllowAnyMethod().AllowAnyHeader().AllowAnyOrigin());

app.MapPost("/orders/create", async (CreateOrderDto request, ApplicationDbContext context, CancellationToken token) =>
{
    Order order = request.Adapt<Order>();
    order.CreatedAt=DateTimeOffset.Now;
    context.Add(order);

    OrderOutbox orderOutbox = new()
    {
        OrderId = order.Id,
        CreateAt = DateTimeOffset.Now,
    };
    context.Add(orderOutbox);

    await context.SaveChangesAsync(token);

    return Results.Ok(Result<string>.Succeed("Order has been created successfully")); // Results.Created()
})
    .Produces<Result<string>>();

app.MapGet("/orders/getall", async (ApplicationDbContext context, CancellationToken token) =>
{
    List<Order>Orders=await context.Orders.ToListAsync(token);

    return Results.Ok(Orders);
})
    .Produces<List<Order>>();


app.Run();

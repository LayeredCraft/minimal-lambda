#if EXPECT_EVENT_API
using MinimalLambda.EventProcessing;
#endif
using MinimalLambda.Builder;
await using var app = LambdaApplication.CreateBuilder().Build();
app.MapHandler(() => Task.CompletedTask);
Console.WriteLine("core-only mapped");

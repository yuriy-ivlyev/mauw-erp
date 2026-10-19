using Mauw.Erp.DataGenerator;
using Microsoft.Extensions.DependencyInjection;

using var services = new ServiceCollection().AddDataGenerator().BuildServiceProvider();
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
return await services.GetRequiredService<GeneratorRunner>().RunAsync(args, cancellation.Token);

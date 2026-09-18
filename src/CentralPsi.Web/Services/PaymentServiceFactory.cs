using Microsoft.Extensions.DependencyInjection;

namespace CentralPsi.Web.Services;

/// <summary>Resolves the right IPaymentService for a given provider name at request time, since the patient
/// now chooses between Flow and Transbank at checkout instead of the app having a single fixed provider.</summary>
public interface IPaymentServiceFactory
{
    IPaymentService Get(string provider);
}

public class PaymentServiceFactory : IPaymentServiceFactory
{
    private readonly IServiceProvider _serviceProvider;

    public PaymentServiceFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IPaymentService Get(string provider) => provider switch
    {
        "Flow" => _serviceProvider.GetRequiredService<FlowPaymentService>(),
        "Transbank" => _serviceProvider.GetRequiredService<TransbankWebpayService>(),
        _ => throw new ArgumentException($"Medio de pago desconocido: {provider}", nameof(provider))
    };
}

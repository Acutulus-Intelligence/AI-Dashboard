using Application.DTos.Response;
using Application.Interfaces;
using Domain.Enums;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace Infrastructure.Payment;

public class StripeService : IPaymentService
{
    private StripeClient? _stripeClient;
    private readonly string _secretKey;
    private readonly string _webhookSecret;

    public StripeService(IOptions<StripeSettings> settings)
    {
        _secretKey = settings.Value.SecretKey;
        _webhookSecret = settings.Value.WebhookSecret;
    }

    private StripeClient StripeClient => _stripeClient ??= new StripeClient(_secretKey);

    private static string AppendSessionIdTemplate(string url)
    {
        var separator = url.Contains('?') ? '&' : '?';
        return $"{url}{separator}session_id={{CHECKOUT_SESSION_ID}}";
    }

    public async Task<CheckoutResponse> CreateCheckoutSessionAsync(
        string customerId,
        Guid userId,
        Guid planId,
        string planName,
        decimal price,
        string? priceId,
        BillingPeriod billingPeriod,
        int trialDays,
        string successUrl,
        string cancelUrl,
        CancellationToken ct = default)
    {
        var metadata = new Dictionary<string, string>
        {
            { "userId", userId.ToString() },
            { "planId", planId.ToString() },
            { "billingPeriod", billingPeriod.ToString() },
            { "trialDays", trialDays.ToString() },
            { "isCompany", "false" }
        };

        var options = new SessionCreateOptions
        {
            Customer = customerId,
            Mode = "subscription",
            SuccessUrl = AppendSessionIdTemplate(successUrl),
            CancelUrl = cancelUrl,
            Metadata = metadata,
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                TrialPeriodDays = trialDays,
                Metadata = metadata
            },
            LineItems =
            [
                BuildLineItem(planName, price, priceId, billingPeriod)
            ]
        };

        var service = new SessionService(StripeClient);
        var session = await service.CreateAsync(options, cancellationToken: ct);
        return new CheckoutResponse(session.Url, session.Id);
    }

    public async Task<CheckoutResponse> CreateCompanyCheckoutSessionAsync(
        string customerId,
        Guid userId,
        Guid companyId,
        Guid planId,
        string planName,
        decimal price,
        string? priceId,
        BillingPeriod billingPeriod,
        int trialDays,
        string successUrl,
        string cancelUrl,
        CancellationToken ct = default)
    {
        var metadata = new Dictionary<string, string>
        {
            { "userId", userId.ToString() },
            { "companyId", companyId.ToString() },
            { "planId", planId.ToString() },
            { "billingPeriod", billingPeriod.ToString() },
            { "trialDays", trialDays.ToString() },
            { "isCompany", "true" }
        };

        var options = new SessionCreateOptions
        {
            Customer = customerId,
            Mode = "subscription",
            SuccessUrl = AppendSessionIdTemplate(successUrl),
            CancelUrl = cancelUrl,
            Metadata = metadata,
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                TrialPeriodDays = trialDays,
                Metadata = metadata
            },
            LineItems =
            [
                BuildLineItem(planName, price, priceId, billingPeriod)
            ]
        };

        var service = new SessionService(StripeClient);
        var session = await service.CreateAsync(options, cancellationToken: ct);
        return new CheckoutResponse(session.Url, session.Id);
    }

    private static SessionLineItemOptions BuildLineItem(
        string planName,
        decimal price,
        string? priceId,
        BillingPeriod billingPeriod)
    {
        if (!string.IsNullOrWhiteSpace(priceId))
        {
            return new SessionLineItemOptions
            {
                Price = priceId,
                Quantity = 1
            };
        }

        return new SessionLineItemOptions
        {
            PriceData = new SessionLineItemPriceDataOptions
            {
                Currency = "usd",
                UnitAmountDecimal = price * 100,
                ProductData = new SessionLineItemPriceDataProductDataOptions
                {
                    Name = planName
                },
                Recurring = new SessionLineItemPriceDataRecurringOptions
                {
                    Interval = billingPeriod == BillingPeriod.Monthly ? "month" : "year"
                }
            },
            Quantity = 1
        };
    }

    public async Task<PaymentWebhookEvent?> RetrieveCheckoutSessionAsync(string sessionId, CancellationToken ct = default)
    {
        var service = new SessionService(StripeClient);
        var session = await service.GetAsync(sessionId, cancellationToken: ct);

        if (session.PaymentStatus != "paid" && session.PaymentStatus != "no_payment_required")
            return null;

        return new PaymentWebhookEvent(
            "checkout.session.completed",
            session.Metadata,
            session.SubscriptionId,
            session.CustomerId,
            null
        );
    }

    public async Task<PaymentWebhookEvent> HandleWebhookAsync(string body, string signature)
    {
        var stripeEvent = EventUtility.ConstructEvent(body, signature, _webhookSecret);

        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
            {
                var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
                if (session is null)
                    break;

                return new PaymentWebhookEvent(
                    stripeEvent.Type,
                    session.Metadata,
                    session.SubscriptionId,
                    session.CustomerId,
                    null
                );
            }

            case "invoice.paid":
            {
                var invoice = stripeEvent.Data.Object as Invoice;
                if (invoice is null)
                    break;

                var subscriptionId = invoice.Parent?.SubscriptionDetails?.SubscriptionId;

                return new PaymentWebhookEvent(
                    stripeEvent.Type,
                    new Dictionary<string, string>(),
                    subscriptionId,
                    invoice.CustomerId
                );
            }

            case "customer.subscription.created":
            {
                var subscription = stripeEvent.Data.Object as Subscription;
                if (subscription is null)
                    break;

                return new PaymentWebhookEvent(
                    stripeEvent.Type,
                    subscription.Metadata,
                    subscription.Id,
                    subscription.CustomerId
                );
            }

            case "customer.subscription.deleted":
            {
                var subscription = stripeEvent.Data.Object as Subscription;
                if (subscription is null)
                    break;

                return new PaymentWebhookEvent(
                    stripeEvent.Type,
                    new Dictionary<string, string>(),
                    subscription.Id,
                    null
                );
            }
        }

        return new PaymentWebhookEvent(stripeEvent.Type, new Dictionary<string, string>(), null, null);
    }

    public async Task CancelSubscriptionAtPeriodEndAsync(string stripeSubscriptionId, CancellationToken ct = default)
    {
        var options = new SubscriptionUpdateOptions
        {
            CancelAtPeriodEnd = true
        };

        var service = new SubscriptionService(StripeClient);
        await service.UpdateAsync(stripeSubscriptionId, options, cancellationToken: ct);
    }

    public async Task CancelSubscriptionImmediatelyAsync(string stripeSubscriptionId, CancellationToken ct = default)
    {
        var service = new SubscriptionService(StripeClient);
        await service.CancelAsync(stripeSubscriptionId, cancellationToken: ct);
    }

    public async Task SwitchSubscriptionPriceAsync(string stripeSubscriptionId, string priceId, CancellationToken ct = default)
    {
        var service = new SubscriptionService(StripeClient);
        var subscription = await service.GetAsync(stripeSubscriptionId, cancellationToken: ct);
        var item = subscription.Items?.Data?.FirstOrDefault();
        if (item is null)
            return;

        var options = new SubscriptionUpdateOptions
        {
            ProrationBehavior = "create_prorations",
            Items = new List<SubscriptionItemOptions>
            {
                new() { Id = item.Id, Price = priceId }
            }
        };

        await service.UpdateAsync(stripeSubscriptionId, options, cancellationToken: ct);
    }

    public async Task<string?> GetPaymentMethodFingerprintAsync(string paymentMethodId, CancellationToken ct = default)
    {
        var service = new PaymentMethodService(StripeClient);
        var pm = await service.GetAsync(paymentMethodId, cancellationToken: ct);
        return pm.Card?.Fingerprint;
    }

    public async Task<string?> GetPaymentMethodFingerprintBySubscriptionAsync(string stripeSubscriptionId, CancellationToken ct = default)
    {
        var options = new SubscriptionGetOptions
        {
            Expand = ["default_payment_method"]
        };

        var service = new SubscriptionService(StripeClient);
        var subscription = await service.GetAsync(stripeSubscriptionId, options, cancellationToken: ct);

        return subscription.DefaultPaymentMethod?.Card?.Fingerprint;
    }

    public async Task EndTrialImmediatelyAsync(string stripeSubscriptionId, CancellationToken ct = default)
    {
        var options = new SubscriptionUpdateOptions
        {
            TrialEnd = DateTime.UtcNow
        };

        var service = new SubscriptionService(StripeClient);
        await service.UpdateAsync(stripeSubscriptionId, options, cancellationToken: ct);
    }

    public async Task UpdateSubscriptionTrialEndAsync(string stripeSubscriptionId, DateTime trialEnd, CancellationToken ct = default)
    {
        var options = new SubscriptionUpdateOptions
        {
            TrialEnd = trialEnd
        };

        var service = new SubscriptionService(StripeClient);
        await service.UpdateAsync(stripeSubscriptionId, options, cancellationToken: ct);
    }

    public async Task<string> GetOrCreateCustomerAsync(string email, Guid userId, CancellationToken ct = default)
    {
        var customerService = new CustomerService(StripeClient);

        var existingCustomers = await customerService.SearchAsync(new CustomerSearchOptions
        {
            Query = $"email:'{email}'"
        }, cancellationToken: ct);

        if (existingCustomers.Any())
            return existingCustomers.First().Id;

        var customer = await customerService.CreateAsync(new CustomerCreateOptions
        {
            Email = email,
            Metadata = new Dictionary<string, string>
            {
                { "userId", userId.ToString() }
            }
        }, cancellationToken: ct);

        return customer.Id;
    }

    public async Task<string> EnsureCustomerExistsAsync(string customerId, string email, Guid userId, CancellationToken ct = default)
    {
        try
        {
            var customerService = new CustomerService(StripeClient);
            var customer = await customerService.GetAsync(customerId, cancellationToken: ct);
            if (customer.Deleted == true)
                return await GetOrCreateCustomerAsync(email, userId, ct);
            return customerId;
        }
        catch (StripeException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return await GetOrCreateCustomerAsync(email, userId, ct);
        }
    }

    public async Task<string> CreateProductAsync(string name, string planId, CancellationToken ct = default)
    {
        var service = new ProductService(StripeClient);
        var product = await service.CreateAsync(new ProductCreateOptions
        {
            Name = name,
            Metadata = new Dictionary<string, string>
            {
                { "planId", planId }
            }
        }, cancellationToken: ct);

        return product.Id;
    }

    public async Task<string> CreatePriceAsync(string productId, decimal amount, BillingPeriod billingPeriod, CancellationToken ct = default)
    {
        var service = new PriceService(StripeClient);
        var price = await service.CreateAsync(new PriceCreateOptions
        {
            Product = productId,
            Currency = "usd",
            UnitAmountDecimal = amount * 100,
            Recurring = new PriceRecurringOptions
            {
                Interval = billingPeriod == BillingPeriod.Monthly ? "month" : "year"
            }
        }, cancellationToken: ct);

        return price.Id;
    }

    public async Task UpdateProductAsync(string productId, string name, CancellationToken ct = default)
    {
        var service = new ProductService(StripeClient);
        await service.UpdateAsync(productId, new ProductUpdateOptions
        {
            Name = name
        }, cancellationToken: ct);
    }

    public async Task DeactivateProductAsync(string productId, CancellationToken ct = default)
    {
        var service = new ProductService(StripeClient);
        await service.UpdateAsync(productId, new ProductUpdateOptions
        {
            Active = false
        }, cancellationToken: ct);
    }

    public async Task ActivateProductAsync(string productId, CancellationToken ct = default)
    {
        var service = new ProductService(StripeClient);
        await service.UpdateAsync(productId, new ProductUpdateOptions
        {
            Active = true
        }, cancellationToken: ct);
    }

    public async Task DeactivatePriceAsync(string priceId, CancellationToken ct = default)
    {
        var service = new PriceService(StripeClient);
        await service.UpdateAsync(priceId, new PriceUpdateOptions
        {
            Active = false
        }, cancellationToken: ct);
    }
}

using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace CyberLms.Web.Services;

public static class CultureSetup
{
    public const string CookieName = "CyberLms.Culture";
    public static readonly CultureInfo[] Supported = [CultureInfo.GetCultureInfo("ar-SA"), CultureInfo.GetCultureInfo("en-US")];

    /// <summary>
    /// ar-SA text/UI resources, but Gregorian dates and Western (0-9) digits and separators, so stored UTC values and numbers
    /// stay unambiguous. (ar-SA's default calendar is Hijri.)
    /// </summary>
    public static CultureInfo FormattingCulture(CultureInfo ui)
    {
        var c = (CultureInfo)ui.Clone();
        var inv = CultureInfo.InvariantCulture.NumberFormat;
        c.NumberFormat.NativeDigits = inv.NativeDigits;
        c.NumberFormat.DigitSubstitution = DigitShapes.None;
        c.NumberFormat.NumberDecimalSeparator = ".";
        c.NumberFormat.NumberGroupSeparator = ",";
        c.NumberFormat.PercentDecimalSeparator = ".";
        c.NumberFormat.PercentSymbol = "%";
        c.NumberFormat.PercentPositivePattern = 1;
        c.NumberFormat.PercentNegativePattern = 1;
        if (c.DateTimeFormat.Calendar is not GregorianCalendar)
        {
            var g = c.OptionalCalendars.OfType<GregorianCalendar>().FirstOrDefault() ?? new GregorianCalendar();
            c.DateTimeFormat.Calendar = g;
        }
        c.DateTimeFormat.DateSeparator = "/"; c.DateTimeFormat.TimeSeparator = ":"; // ar-SA inserts invisible RLM marks around separators
        return c;
    }

    public static void Configure(RequestLocalizationOptions o, SettingsService settings)
    {
        o.DefaultRequestCulture = new RequestCulture("ar-SA");
        o.SupportedCultures = Supported; o.SupportedUICultures = Supported;
        o.RequestCultureProviders.Clear();
        o.RequestCultureProviders.Add(new CookieRequestCultureProvider { CookieName = CookieName });
        // Administrator-chosen default language (Settings), otherwise Arabic.
        o.RequestCultureProviders.Add(new CustomRequestCultureProvider(_ =>
            Task.FromResult<ProviderCultureResult?>(settings.Get("General.DefaultLanguage") == "en" ? new ProviderCultureResult("en-US") : null)));
    }
}

/// <summary>Localises validation messages and display names (DataAnnotations) and model-binding errors.</summary>
public class LocalizedMvcSetup(IStringLocalizerFactory factory) : IPostConfigureOptions<MvcOptions>
{
    // Post-configure so our provider runs after the built-in DataAnnotations provider has collected the attributes.
    public void PostConfigure(string? name, MvcOptions o)
    {
        var loc = factory.Create(typeof(SharedResource));
        o.ModelMetadataDetailsProviders.Add(new LocalizedValidationProvider());
        var p = o.ModelBindingMessageProvider;
        p.SetValueMustNotBeNullAccessor(_ => loc["The value is required."].Value);
        p.SetMissingBindRequiredValueAccessor(n => string.Format(loc["A value for the {0} parameter is required."].Value, n));
        p.SetMissingKeyOrValueAccessor(() => loc["A value is required."].Value);
        p.SetAttemptedValueIsInvalidAccessor((v, n) => string.Format(loc["The value '{0}' is not valid for {1}."].Value, v, n));
        p.SetUnknownValueIsInvalidAccessor(n => string.Format(loc["The value provided for {0} is invalid."].Value, n));
        p.SetValueIsInvalidAccessor(v => string.Format(loc["The value '{0}' is invalid."].Value, v));
        p.SetValueMustBeANumberAccessor(n => string.Format(loc["The field {0} must be a number."].Value, n));
        p.SetNonPropertyValueMustBeANumberAccessor(() => loc["The field must be a number."].Value);
    }
}

/// <summary>Gives attributes without a message an (English-keyed) message so AddDataAnnotationsLocalization can translate it.</summary>
public class LocalizedValidationProvider : IValidationMetadataProvider
{
    public void CreateValidationMetadata(ValidationMetadataProviderContext context)
    {
        foreach (var meta in context.ValidationMetadata.ValidatorMetadata)
        {
            if (meta is not ValidationAttribute { ErrorMessage: null, ErrorMessageResourceName: null } va) continue;
            va.ErrorMessage = va switch
            {
                RequiredAttribute => "The {0} field is required.",
                StringLengthAttribute => "The field {0} must be at most {1} characters.",
                MaxLengthAttribute => "The field {0} must be at most {1} characters.",
                RangeAttribute => "The field {0} must be between {1} and {2}.",
                EmailAddressAttribute => "The field {0} is not a valid e-mail address.",
                CompareAttribute => "{0} and {1} do not match.",
                _ => null,
            };
        }
    }
}

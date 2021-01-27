using NLog;
using RadiusR.API.CustomerWebService.Enums;
using RadiusR.API.CustomerWebService.Localization;
using RadiusR.DB.Utilities.Billing;
using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService
{
    public static class CommonResponse
    {
        public static ServiceResponse InternalException(string culture, Exception ex = null)
        {
            var msg = ex == null ? "" : ex.Message;
            return new ServiceResponse()
            {
                ErrorCode = (int)ErrorCodes.InternalServerError,
                ErrorMessage = new RezaB.Data.Localization.LocalizedList<ErrorCodes, ErrorMessages>().GetDisplayText((int)ErrorCodes.InternalServerError, CreateCulture(culture)) + $" - { msg }"
            };
        }
        public static ServiceResponse UnauthorizedResponse(BaseRequest<SHA1> request)
        {
            Logger Errorslogger = LogManager.GetLogger("Errors");
            Errorslogger.Error($"Unauthorize error. User : {request.Username}");
            return new ServiceResponse()
            {
                ErrorCode = (int)ErrorCodes.AuthenticationFailed,
                ErrorMessage = new RezaB.Data.Localization.LocalizedList<ErrorCodes, ErrorMessages>().GetDisplayText((int)ErrorCodes.AuthenticationFailed, CreateCulture(request.Culture))
            };
        }
        public static ServiceResponse SubscriberNotFoundErrorResponse(string culture)
        {
            return new ServiceResponse()
            {
                ErrorCode = (int)ErrorCodes.SubscriberNotFound,
                ErrorMessage = new RezaB.Data.Localization.LocalizedList<ErrorCodes, ErrorMessages>().GetDisplayText((int)ErrorCodes.SubscriberNotFound, CreateCulture(culture))
            };
        }
        public static ServiceResponse ClientNotFound(string culture)
        {
            return new ServiceResponse()
            {
                ErrorCode = (int)ErrorCodes.ClientNotFound,
                ErrorMessage = new RezaB.Data.Localization.LocalizedList<ErrorCodes, Localization.Common>().GetDisplayText((int)ErrorCodes.ClientNotFound, CreateCulture(culture))
            };
        }
        public static ServiceResponse NullObjectException(string culture)
        {
            return new ServiceResponse()
            {
                ErrorCode = (int)ErrorCodes.NullObjectFound,
                ErrorMessage = new RezaB.Data.Localization.LocalizedList<ErrorCodes, ErrorMessages>().GetDisplayText((int)ErrorCodes.NullObjectFound, CreateCulture(culture))
            };
        }
        public static ServiceResponse BillsNotFoundException(string culture)
        {
            return new ServiceResponse()
            {
                ErrorCode = (int)ErrorCodes.BillsNotFound,
                ErrorMessage = new RezaB.Data.Localization.LocalizedList<ErrorCodes, ErrorMessages>().GetDisplayText((int)ErrorCodes.BillsNotFound, CreateCulture(culture))
            };
        }
        public static ServiceResponse SuccessResponse(string culture, string successMessage = null)
        {
            return new ServiceResponse()
            {
                ErrorCode = (int)ErrorCodes.Success,
                ErrorMessage = successMessage ?? new RezaB.Data.Localization.LocalizedList<ErrorCodes, ErrorMessages>().GetDisplayText((int)ErrorCodes.Success, CreateCulture(culture))
            };
        }
        public static ServiceResponse FailedResponse(string culture, string errorMessage = null, int? errorCode = null)
        {
            return new ServiceResponse()
            {
                ErrorCode = errorCode ?? (int)ErrorCodes.Failed,
                ErrorMessage = !string.IsNullOrEmpty(errorMessage) ? errorMessage : new RezaB.Data.Localization.LocalizedList<ErrorCodes, ErrorMessages>().GetDisplayText((int)ErrorCodes.Failed, CreateCulture(culture))
            };
        }
        public static ServiceResponse HasActiveRequest(string culture)
        {
            return new ServiceResponse()
            {
                ErrorCode = (int)ErrorCodes.HasActiveRequest,
                ErrorMessage = new RezaB.Data.Localization.LocalizedList<ErrorCodes, Localization.Common>().GetDisplayText((int)ErrorCodes.HasActiveRequest, CreateCulture(culture))
            };
        }
        public static ServiceResponse InvalidOperation(string culture)
        {
            return new ServiceResponse()
            {
                ErrorCode = (int)ErrorCodes.InvalidOperation,
                ErrorMessage = new RezaB.Data.Localization.LocalizedList<ErrorCodes, ErrorMessages>().GetDisplayText((int)ErrorCodes.InvalidOperation, CreateCulture(culture))
            };
        }
        public static ServiceResponse TelekomCredentialNotFound(string culture)
        {
            return new ServiceResponse()
            {
                ErrorCode = (int)ErrorCodes.TelekomCredentialsNotFound,
                ErrorMessage = new RezaB.Data.Localization.LocalizedList<ErrorCodes, ErrorMessages>().GetDisplayText((int)ErrorCodes.TelekomCredentialsNotFound, CreateCulture(culture))
            };
        }
        public static ServiceResponse TelekomWebServiceError(string culture, string errorMessage = null)
        {
            return new ServiceResponse()
            {
                ErrorCode = (int)ErrorCodes.TelekomWebServiceError,
                ErrorMessage = new RezaB.Data.Localization.LocalizedList<ErrorCodes, ErrorMessages>().GetDisplayText((int)ErrorCodes.TelekomWebServiceError, CreateCulture(culture)) + $" - {errorMessage}"
            };
        }
        public static ServiceResponse EArchivePDFNotFound(string culture, string errorMessage = null)
        {
            return new ServiceResponse()
            {
                ErrorCode = (int)ErrorCodes.EArchivePDFNotFound,
                ErrorMessage = new RezaB.Data.Localization.LocalizedList<ErrorCodes, ErrorMessages>().GetDisplayText((int)ErrorCodes.EArchivePDFNotFound, CreateCulture(culture)) + $" - {errorMessage}"
            };
        }
        public static ServiceResponse QuotaNotFound(string culture, string errorMessage = null)
        {
            return new ServiceResponse()
            {
                ErrorCode = (int)ErrorCodes.QuotaNotFound,
                ErrorMessage = new RezaB.Data.Localization.LocalizedList<ErrorCodes, ErrorMessages>().GetDisplayText((int)ErrorCodes.QuotaNotFound, CreateCulture(culture)) + $" - {errorMessage}"
            };
        }
        public static ServiceResponse QuotaTariffNotFound(string culture, string errorMessage = null)
        {
            return new ServiceResponse()
            {
                ErrorCode = (int)ErrorCodes.QuotaTariffNotFound,
                ErrorMessage = new RezaB.Data.Localization.LocalizedList<ErrorCodes, ErrorMessages>().GetDisplayText((int)ErrorCodes.QuotaTariffNotFound, CreateCulture(culture)) + $" - {errorMessage}"
            };
        }
        public static ServiceResponse MobilexpressIsDeactive(string culture, string errorMessage = null)
        {
            return new ServiceResponse()
            {
                ErrorCode = (int)ErrorCodes.MobilexpressIsDeactive,
                ErrorMessage = new RezaB.Data.Localization.LocalizedList<ErrorCodes, ErrorMessages>().GetDisplayText((int)ErrorCodes.MobilexpressIsDeactive, CreateCulture(culture)) + $" - {errorMessage}"
            };
        }
        public static ServiceResponse WrongSMSValidation(string culture, string errorMessage = null)
        {
            return new ServiceResponse()
            {
                ErrorCode = (int)ErrorCodes.WrongSMSValidation,
                ErrorMessage = new RezaB.Data.Localization.LocalizedList<ErrorCodes, ErrorMessages>().GetDisplayText((int)ErrorCodes.WrongSMSValidation, CreateCulture(culture)) + $" - {errorMessage}"
            };
        }
        public static ServiceResponse HasActiveAutoPayment(string culture, string errorMessage = null)
        {
            return new ServiceResponse()
            {
                ErrorCode = (int)ErrorCodes.HasActiveAutoPayment,
                ErrorMessage = new RezaB.Data.Localization.LocalizedList<ErrorCodes, ErrorMessages>().GetDisplayText((int)ErrorCodes.HasActiveAutoPayment, CreateCulture(culture)) + errorMessage == null ? "" : $" - {errorMessage}"
            };
        }
        public static ServiceResponse CardInformationNotFound(string culture)
        {
            return new ServiceResponse()
            {
                ErrorCode = (int)ErrorCodes.CardInformationNotFound,
                ErrorMessage = new RezaB.Data.Localization.LocalizedList<ErrorCodes, ErrorMessages>().GetDisplayText((int)ErrorCodes.CardInformationNotFound, CreateCulture(culture))
            };
        }
        public static ServiceResponse PaymentResponse(string culture, BillPayment.ResponseType responseType)
        {
            return new ServiceResponse()
            {
                ErrorCode = (int)ErrorCodes.Failed,
                ErrorMessage = new RezaB.Data.Localization.LocalizedList<BillPayment.ResponseType, ErrorMessages>().GetDisplayText((int)responseType, CreateCulture(culture))
            };
        }
        public static ServiceResponse SpecialOfferError(string culture)
        {
            return new ServiceResponse()
            {
                ErrorCode = (int)ErrorCodes.SpecialOfferError,
                ErrorMessage = new RezaB.Data.Localization.LocalizedList<ErrorCodes, ErrorMessages>().GetDisplayText((int)ErrorCodes.SpecialOfferError, CreateCulture(culture))
            };
        }
        public static ServiceResponse TariffNotFound(string culture)
        {
            return new ServiceResponse()
            {
                ErrorCode = (int)ErrorCodes.TariffNotFound,
                ErrorMessage = new RezaB.Data.Localization.LocalizedList<ErrorCodes, ErrorMessages>().GetDisplayText((int)ErrorCodes.TariffNotFound, CreateCulture(culture))
            };
        }
        public static ServiceResponse SupportRequestNotFound(string culture)
        {
            return new ServiceResponse()
            {
                ErrorCode = (int)ErrorCodes.SupportRequestNotFound,
                ErrorMessage = new RezaB.Data.Localization.LocalizedList<ErrorCodes, ErrorMessages>().GetDisplayText((int)ErrorCodes.SupportRequestNotFound, CreateCulture(culture))
            };
        }
        public static ServiceResponse CustomerMailNotFound(string culture)
        {
            return new ServiceResponse()
            {
                ErrorCode = (int)ErrorCodes.CustomerMailNotFound,
                ErrorMessage = new RezaB.Data.Localization.LocalizedList<ErrorCodes, ErrorMessages>().GetDisplayText((int)ErrorCodes.CustomerMailNotFound, CreateCulture(culture))
            };
        }
        private static CultureInfo CreateCulture(string cultureName)
        {
            var currentCulture = CultureInfo.InvariantCulture;
            try
            {
                currentCulture = CultureInfo.CreateSpecificCulture(cultureName);
            }
            catch { }
            return currentCulture;
        }
    }
}
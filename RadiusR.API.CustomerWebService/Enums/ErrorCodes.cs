using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

/// <summary>
/// Summary description for ErrorCodes
/// </summary>
/// 
namespace RadiusR.API.CustomerWebService.Enums
{
    public enum ErrorCodes
    {
        Success = 0,
        AuthenticationFailed = 1,
        SubscriberNotFound = 2,
        NullObjectFound = 3,
        BillsNotFound = 4,
        HasActiveRequest = 5,
        InvalidOperation = 6,
        TelekomCredentialsNotFound = 7,
        TelekomWebServiceError = 8,
        EArchivePDFNotFound = 9,
        QuotaNotFound = 10,
        QuotaTariffNotFound = 11,
        MobilexpressIsDeactive = 12,
        WrongSMSValidation = 13,
        HasActiveAutoPayment = 14,
        CardInformationNotFound = 15,
        ClientNotFound = 16,
        SpecialOfferError = 17,
        TariffNotFound = 18,
        SupportRequestNotFound = 19,
        CustomerMailNotFound = 20,
        InternalServerError = 199,
        Failed = 200
    }
}

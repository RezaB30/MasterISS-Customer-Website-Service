using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace RadiusR.API.CustomerWebService.Enums
{
    public enum AgentErrorCodes
    {
        PaymentPermissionNotFound = 1,
        NotEnoughCredit = 2,
        PartnerIsNotActive = 3,
        MaxSubUsersReached = 4,
        SubUserExists = 5,
        AuthenticationFailed = 6,
        NoPermission = 7,
        PartnerNotFound = 8,
        SubscriberNotFound = 9,
        InvalidPhoneNo = 10,
        SpecialOfferError = 17,
        TariffNotFound = 18,
        InternalServerError = 199,
        Failed = 200
    }
}
using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses.Payment
{
    //public class RemoveCardSMSValidationResponse
    //{
    //}
    [DataContract]
    public partial class CustomerServiceRemoveCardSMSValidationResponse : BaseResponse<string, SHA1>
    {
        public CustomerServiceRemoveCardSMSValidationResponse(string passwordHash, BaseRequest<SHA1> request) : base(passwordHash, request) { }

        [DataMember]
        public string SMSCode
        {
            get { return Data; }
            set { Data = value; }
        }

    }
}
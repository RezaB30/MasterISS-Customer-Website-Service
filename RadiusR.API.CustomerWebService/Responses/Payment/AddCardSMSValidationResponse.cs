using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses.Payment
{
    //[DataContract]
    //public class AddCardSMSValidationResponse
    //{
    //    [DataMember]
    //    public string SMSCode { get; set; }
    //}
    [DataContract]
    public partial class CustomerServiceAddCardSMSValidationResponse : BaseResponse<string, SHA1>
    {
        public CustomerServiceAddCardSMSValidationResponse(string passwordHash, BaseRequest<SHA1> request) : base(passwordHash, request) { }

        [DataMember]
        public string SMSCode
        {
            get { return Data; }
            set { Data = value; }
        }

    }
}
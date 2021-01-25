using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses
{
    [DataContract]
    public class HasPreRegisterResponse
    {
        [DataMember]
        public bool HasPreRegister { get; set; }
        [DataMember]
        public bool IsCurrentPreRegister { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceHasClientPreRegisterResponse : BaseResponse<HasPreRegisterResponse, SHA1>
    {
        public CustomerServiceHasClientPreRegisterResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public HasPreRegisterResponse HasClientPreRegister { get { return Data; } set { Data = value; } }
    }
}
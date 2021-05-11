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
    public partial class CustomerServiceAppLogResponse : BaseResponse<bool, SHA1>
    {
        public CustomerServiceAppLogResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public bool AppLogResult { get { return Data; } set { Data = value; } }
    }
}
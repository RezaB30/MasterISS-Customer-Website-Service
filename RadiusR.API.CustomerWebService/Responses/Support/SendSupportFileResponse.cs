﻿using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses.Support
{
    [DataContract]
    public partial class CustomerServiceSendSupportFileResponse : BaseResponse<bool?, SHA1>
    {
        public CustomerServiceSendSupportFileResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public bool? SendSupportFileResult { get { return Data; } set { Data = value; } }
    }
}
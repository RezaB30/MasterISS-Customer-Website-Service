﻿using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses.Payment
{
    [DataContract]
    public partial class CustomerServicePaymentSystemLogResponse : BaseResponse<bool?, SHA1>
    {
        public CustomerServicePaymentSystemLogResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public bool? PaymentSystemLogResult { get { return Data; } set { Data = value; } }
    }
}
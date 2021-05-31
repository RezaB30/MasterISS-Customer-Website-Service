using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Requests.Payment
{
    [DataContract]
    public partial class CustomerServicePaymentTypeListRequest : BaseRequest<SHA1> { }
}
using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Requests
{
    [DataContract]
    public partial class CustomerServiceQuotaPackagesRequest : BaseRequest<SHA1> { }
    [DataContract]
    public partial class CustomerServiceVPOSErrorParameterNameRequest : BaseRequest<SHA1> { }
    [DataContract]
    public partial class CustomerServiceCommitmentLengthsRequest : BaseRequest<SHA1> { }
    [DataContract]
    public partial class CustomerServiceProvincesRequest : BaseRequest<SHA1> { }
}
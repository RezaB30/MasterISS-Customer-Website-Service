using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

/// <summary>
/// Summary description for EArchivePDFRequest
/// </summary>
/// 
namespace RadiusR.API.CustomerWebService.Requests.Payment
{
    [DataContract]
    public class EArchivePDFRequest : BaseSubscriptionRequest
    {
        [DataMember]
        public long? BillId { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceEArchivePDFRequest : BaseRequest<EArchivePDFRequest, SHA1>
    {
        [DataMember]
        public EArchivePDFRequest EArchivePDFParameters
        {
            get
            {
                return Data;
            }
            set
            {
                Data = value;
            }
        }
    }
}

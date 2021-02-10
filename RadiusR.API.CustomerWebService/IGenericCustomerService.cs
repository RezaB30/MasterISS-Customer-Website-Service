using RadiusR.API.CustomerWebService.Requests;
using RadiusR.API.CustomerWebService.Requests.Payment;
using RadiusR.API.CustomerWebService.Requests.Support;
using RadiusR.API.CustomerWebService.Responses;
using RadiusR.API.CustomerWebService.Responses.Payment;
using RadiusR.API.CustomerWebService.Responses.Support;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;

namespace RadiusR.API.CustomerWebService
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the interface name "IService1" in both code and config file together.
    [ServiceContract]    
    public interface IGenericCustomerService : RadiusR.API.TelekomInfrastructureService.ITelekomInfrastructureService
    {
        [OperationContract]
        string GetKeyFragment(string username);
        [OperationContract]
        CustomerServiceGetCustomerInfoResponse GetCustomerInfo(CustomerServiceBaseRequest request);
        [OperationContract]
        CustomerServiceGetCustomerBillsResponse GetCustomerBills(CustomerServiceBaseRequest request);
        [OperationContract]
        CustomerServiceCustomerAuthenticationResponse CustomerAuthentication(CustomerServiceAuthenticationRequest request);
        [OperationContract]
        CustomerServiceGetCustomerSpecialOffersResponse GetCustomerSpecialOffers(CustomerServiceBaseRequest request);
        [OperationContract]
        CustomerServiceGetCustomerTariffAndTrafficInfoResponse GetCustomerTariffAndTrafficInfo(CustomerServiceBaseRequest request);
        [OperationContract]
        CustomerServiceNameValuePair GetSupportTypes(CustomerServiceSupportTypesRequest request);
        [OperationContract]
        CustomerServiceNameValuePair GetSupportSubTypes(CustomerServiceSupportSubTypesRequest request);
        [OperationContract]
        CustomerServiceGetCustomerSupportListResponse GetSupportList(CustomerServiceBaseRequest request);
        [OperationContract]
        CustomerServiceSupportDetailMessagesResponse GetSupportDetailMessages(CustomerServiceSupportDetailMessagesRequest request);
        [OperationContract]
        CustomerServiceSupportRegisterResponse SupportRegister(CustomerServiceSupportRegisterRequest request);
        [OperationContract]
        CustomerServiceSendSupportMessageResponse SendSupportMessage(CustomerServiceSendSupportMessageRequest request);
        [OperationContract]
        CustomerServiceConnectionStatusResponse ConnectionStatus(CustomerServiceBaseRequest request);
        [OperationContract]
        CustomerServiceEArchivePDFResponse EArchivePDF(CustomerServiceEArchivePDFRequest request);
        [OperationContract]
        CustomerServicePayBillsResponse PayBills(CustomerServicePayBillsRequest request);
        [OperationContract]
        CustomerServiceVPOSFormResponse GetVPOSForm(CustomerServiceVPOSFormRequest request);
        [OperationContract]
        CustomerServiceQuotaPackagesResponse QuotaPackageList(CustomerServiceQuotaPackagesRequest request);
        [OperationContract]
        CustomerServiceQuotaSaleResponse QuotaSale(CustomerServiceQuotaSaleRequest request);
        [OperationContract]
        CustomerServiceCanHaveQuotaSaleResponse CanHaveQuotaSale(CustomerServiceBaseRequest request);
        [OperationContract]
        CustomerServiceAddCardSMSValidationResponse AddCardSMSCheck(CustomerServiceBaseRequest request);
        [OperationContract]
        CustomerServiceAddCardResponse AddCard(CustomerServiceAddCardRequest request);
        [OperationContract]
        CustomerServiceRegisteredCardsResponse RegisteredMobilexpressCardList(CustomerServiceRegisteredCardsRequest request);
        [OperationContract]
        CustomerServiceRemoveCardSMSValidationResponse RemoveCardSMSCheck(CustomerServiceRemoveCardSMSCheckRequest request);
        [OperationContract]
        CustomerServiceRemoveCardResponse RemoveCard(CustomerServiceRemoveCardRequest request);
        [OperationContract]
        CustomerServiceActivateAutomaticPaymentResponse ActivateAutomaticPayment(CustomerServiceActivateAutomaticPaymentRequest request);
        [OperationContract]
        CustomerServiceDeactivateAutomaticPaymentResponse DeativateAutomaticPayment(CustomerServiceBaseRequest request);
        [OperationContract]
        CustomerServiceAuthenticationSMSConfirmResponse AuthenticationSMSConfirm(CustomerServiceAuthenticationSMSConfirmRequest request);
        [OperationContract]
        CustomerServiceSubscriptionBasicInformationResponse SubscriptionBasicInfo(CustomerServiceBaseRequest request);
        [OperationContract]
        CustomerServiceChangeSubClientResponse ChangeSubClient(CustomerServiceChangeSubClientRequest request);
        [OperationContract]
        CustomerServiceHasActiveRequestResponse SupportHasActiveRequest(CustomerServiceBaseRequest request);
        [OperationContract]
        CustomerServiceSupportStatusResponse SupportStatus(CustomerServiceBaseRequest request);
        //[OperationContract]
        //CustomerServiceDomainCachesResponse DomainsCaches(CustomerServiceDomainCachesRequest request);
        [OperationContract]
        CustomerServiceBillPayableAmountResponse BillPayableAmount(CustomerServiceBillPayableAmountRequest request);
        [OperationContract]
        CustomerServicePaymentTypeListResponse PaymentTypeList(CustomerServicePaymentTypeListRequest request);
        [OperationContract]
        CustomerServiceGenericAppSettingsResponse GenericAppSettings(CustomerServiceGenericAppSettingsRequest request);
        [OperationContract]
        CustomerServiceSendSubscriberSMSResponse SendSubscriberSMS(CustomerServiceSendSubscriberSMSRequest request);
        [OperationContract]
        CustomerServiceAutoPaymentListResponse AutoPaymentList(CustomerServiceAutoPaymentListRequest request);
        [OperationContract]
        CustomerServicePaymentSystemLogResponse PaymentSystemLog(CustomerServicePaymentSystemLogRequest request);
        [OperationContract]
        CustomerServiceMobilexpressPayBillResponse MobilexpressPayBill(CustomerServiceMobilexpressPayBillRequest request);
        [OperationContract]
        CustomerServiceVPOSErrorParameterNameResponse GetVPOSErrorParameterName(CustomerServiceVPOSErrorParameterNameRequest request);
        [OperationContract]
        CustomerServiceExistingCustomerRegisterResponse ExistingCustomerRegister(CustomerServiceExistingCustomerRegisterRequest request);
        [OperationContract]
        CustomerServiceNameValuePair CommitmentLengthList(CustomerServiceCommitmentLengthsRequest request);        
        [OperationContract]
        CustomerServiceExternalTariffResponse ExternalTariffList(CustomerServiceExternalTariffRequest request);        
        [OperationContract]
        CustomerServiceGetCustomerFileResponse GetCustomerFiles(CustomerServiceBaseRequest request);
        [OperationContract]
        CustomerServiceGetClientAttachmentResponse GetClientAttachment(CustomerServiceGetClientAttachmentRequest request);
        [OperationContract]
        CustomerServicGetSupportAttachmentListResponse GetSupportAttachmentList(CustomerServiceGetSupportAttachmentListRequest request);
        [OperationContract]
        CustomerServiceGetSupportAttachmentResponse GetSupportAttachment(CustomerServiceGetSupportAttachmentRequest request);
        [OperationContract]
        CustomerServiceSaveSupportAttachmentResponse SaveSupportAttachment(CustomerServiceSaveSupportAttachmentRequest request);
        [OperationContract]
        CustomerServiceCustomerAuthenticationWithPasswordResponse CustomerAuthenticationWithPassword(CustomerServiceAuthenticationWithPasswordRequest request);
        [OperationContract]
        CustomerServiceHasClientPreRegisterResponse HasClientPreRegisterSubscription(CustomerServiceBaseRequest request);
        [OperationContract]
        CustomerServiceSaveClientAttachmentResponse SaveClientAttachment(CustomerServiceSaveClientAttachmentRequest request);
        [OperationContract]
        CustomerServiceGetClientPDFFormResponse GetClientPDFForm(CustomerServiceBaseRequest request);
        [OperationContract]
        CustomerServiceEArchivePDFMailResponse EArchivePDFMail(CustomerServiceEArchivePDFRequest request);
        [OperationContract]
        CustomerServiceChangeClientInfoResponse ChangeClientInfoSMSCheck(CustomerServiceChangeClientInfoRequest request);
        [OperationContract]
        CustomerServiceChangeClientInfoConfirmResponse ChangeClientInfoConfirm(CustomerServiceChangeClientInfoConfirmRequest request);
        [OperationContract]
        CustomerServiceChangeClientInfoConfirmResponse ChangeClientOnlinePassword(CustomerServiceChangeClientOnlinePasswordRequest request);
    }
}

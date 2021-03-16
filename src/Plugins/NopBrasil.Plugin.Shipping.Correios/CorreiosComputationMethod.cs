using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Plugins;
using Nop.Services.Shipping;
using Nop.Services.Shipping.Tracking;
using NopBrasil.Plugin.Shipping.Correios.Domain;
using NopBrasil.Plugin.Shipping.Correios.Service;

namespace NopBrasil.Plugin.Shipping.Correios
{
    public class CorreiosComputationMethod : BasePlugin, IShippingRateComputationMethod
    {
        private readonly ISettingService _settingService;
        private readonly CorreiosSettings _correiosSettings;
        private readonly ILogger _logger;
        private readonly IWebHelper _webHelper;
        private readonly ILocalizationService _localizationService;
        private readonly ICorreiosService _correiosService;

        public CorreiosComputationMethod(ISettingService settingService,
            CorreiosSettings correiosSettings, ILogger logger, IWebHelper webHelper,
            ILocalizationService localizationService, ICorreiosService correiosService)
        {
            this._settingService = settingService;
            this._correiosSettings = correiosSettings;
            this._logger = logger;
            this._webHelper = webHelper;
            this._localizationService = localizationService;
            this._correiosService = correiosService;
        }

        private async Task<bool> ValidateRequest(GetShippingOptionRequest getShippingOptionRequest, GetShippingOptionResponse response)
        {
            if (getShippingOptionRequest.Items == null)
                response.AddError(await _localizationService.GetResourceAsync("Plugins.Shipping.Correios.Message.NoShipmentItems"));
            if (getShippingOptionRequest.ShippingAddress == null)
                response.AddError(await _localizationService.GetResourceAsync("Plugins.Shipping.Correios.Message.AddressNotSet"));
            if (getShippingOptionRequest.ShippingAddress.CountryId == null)
                response.AddError(await _localizationService.GetResourceAsync("Plugins.Shipping.Correios.Message.CountryNotSet"));
            if (getShippingOptionRequest.ShippingAddress.StateProvinceId == null)
                response.AddError(await _localizationService.GetResourceAsync("Plugins.Shipping.Correios.Message.StateNotSet"));
            if (getShippingOptionRequest.ShippingAddress.ZipPostalCode == null)
                response.AddError(await _localizationService.GetResourceAsync("Plugins.Shipping.Correios.Message.PostalCodeNotSet"));
            return response.Errors.Count <= 0;
        }

        private decimal ApplyAdditionalFee(decimal rate) => _correiosSettings.PercentageShippingFee > 0.0M ? rate * _correiosSettings.PercentageShippingFee : rate;

        private ShippingOption GetShippingOption(decimal rate, string serviceName, int prazo, string obs = null)
        {
            var shippingName = $"{serviceName} - {prazo} dia(s)";
            if (!string.IsNullOrEmpty(obs))
                shippingName += $" - {obs}";
            return new ShippingOption() { Rate = _correiosService.GetConvertedRateToPrimaryCurrency(rate), Name = shippingName };
        }

        private int CalcPrazoEntrega(WSCorreiosCalcPrecoPrazo.cServico serv)
        {
            int prazo = Convert.ToInt32(serv.PrazoEntrega);
            if (_correiosSettings.AddDaysForDelivery > 0)
                prazo += _correiosSettings.AddDaysForDelivery;
            return prazo;
        }

        private async Task<string> ValidateWSResult(WSCorreiosCalcPrecoPrazo.cServico wsServico)
        {
            string retorno = string.Empty;
            if (!string.IsNullOrEmpty(wsServico.Erro) && (wsServico.Erro != "0"))
            {
                if ((wsServico.Erro == "009") || (wsServico.Erro == "010") || (wsServico.Erro == "011"))
                    retorno = wsServico.MsgErro;
                else
                    throw new NopException(wsServico.Erro + " - " + wsServico.MsgErro);
            }

            if (Convert.ToInt32(wsServico.PrazoEntrega) <= 0)
                throw new NopException(await _localizationService.GetResourceAsync("Plugins.Shipping.Correios.Message.DeliveryUninformed"));

            if (Convert.ToDecimal(wsServico.Valor, new CultureInfo("pt-BR")) <= 0)
                throw new NopException(await _localizationService.GetResourceAsync("Plugins.Shipping.Correios.Message.InvalidValueDelivery"));

            return retorno;
        }

        public decimal? GetFixedRate(GetShippingOptionRequest getShippingOptionRequest) => null;

        public override string GetConfigurationPageUrl() => _webHelper.GetStoreLocation() + "Admin/ShippingCorreios/Configure";

        public override async Task InstallAsync()
        {
            var settings = new CorreiosSettings()
            {
                Url = "http://ws.correios.com.br/calculador/CalcPrecoPrazo.asmx",
                PostalCodeFrom = "",
                CompanyCode = "",
                Password = "",
                AddDaysForDelivery = 0,
                PercentageShippingFee = 1.0M,
                DeclaredMinimumValue = 19.5M,
                MinimumWeight = 1,
                MaximumWeight = 30,
                MinimumHeight = 2.0M,
                MinimumLength = 16.0M,
                MinimumWidth = 11.0M,
                MaximumHeight = 105.0M,
                MaximumLength = 105.0M,
                MaximumWidth = 105.0M
            };
            await _settingService.SaveSettingAsync(settings);

            await _localizationService.AddLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Plugins.Shipping.Correios.Fields.Url"] = "URL",
                ["Plugins.Shipping.Correios.Fields.Url.Hint"] = "Specify Correios URL.",
                ["Plugins.Shipping.Correios.Fields.PostalCodeFrom"] = "Postal Code From",
                ["Plugins.Shipping.Correios.Fields.PostalCodeFrom.Hint"] = "Specify From Postal Code.",
                ["Plugins.Shipping.Correios.Fields.CompanyCode"] = "Company Code",
                ["Plugins.Shipping.Correios.Fields.CompanyCode.Hint"] = "Specify Your Company Code.",
                ["Plugins.Shipping.Correios.Fields.Password"] = "Password",
                ["Plugins.Shipping.Correios.Fields.Password.Hint"] = "Specify Your Password.",
                ["Plugins.Shipping.Correios.Fields.AddDaysForDelivery"] = "Additional Days For Delivery",
                ["Plugins.Shipping.Correios.Fields.AddDaysForDelivery.Hint"] = "Set The Amount Of Additional Days For Delivery.",
                ["Plugins.Shipping.Correios.Fields.AvailableCarrierServices"] = "Available Carrier Services",
                ["Plugins.Shipping.Correios.Fields.AvailableCarrierServices.Hint"] = "Set Available Carrier Services.",
                ["Plugins.Shipping.Correios.Fields.ServiceNameDefault"] = "Service Name Default",
                ["Plugins.Shipping.Correios.Fields.ServiceNameDefault.Hint"] = "Service Name Used When The Correios Does Not Return Value.",
                ["Plugins.Shipping.Correios.Fields.ShippingRateDefault"] = "Shipping Rate Default",
                ["Plugins.Shipping.Correios.Fields.ShippingRateDefault.Hint"] = "Shipping Rate Used When The Correios Does Not Return Value.",
                ["Plugins.Shipping.Correios.Fields.QtdDaysForDeliveryDefault"] = "Number Of Days For Delivery Default",
                ["Plugins.Shipping.Correios.Fields.QtdDaysForDeliveryDefault.Hint"] = "Number Of Days For Delivery Used When The Correios Does Not Return Value.",
                ["Plugins.Shipping.Correios.Fields.PercentageShippingFee"] = "Additional percentage shipping fee",
                ["Plugins.Shipping.Correios.Fields.PercentageShippingFee.Hint"] = "Set the additional percentage shipping rate.",

                ["Plugins.Shipping.Correios.Fields.DeclaredMinimumValue"] = "Declared Minimum Value",
                ["Plugins.Shipping.Correios.Fields.DeclaredMinimumValue.Hint"] = "The Minimum Amount Accepted by Correios for Declaration",
                ["Plugins.Shipping.Correios.Fields.MinimumLength"] = "Minimum Length",
                ["Plugins.Shipping.Correios.Fields.MinimumLength.Hint"] = "Set the Minimum Length",
                ["Plugins.Shipping.Correios.Fields.MinimumHeight"] = "Minimum Height",
                ["Plugins.Shipping.Correios.Fields.MinimumHeight.Hint"] = "Set the Minimum Height",
                ["Plugins.Shipping.Correios.Fields.MinimumWidth"] = "Minimum Width",
                ["Plugins.Shipping.Correios.Fields.MinimumWidth.Hint"] = "Set the Minimum Width",
                ["Plugins.Shipping.Correios.Fields.MaximumLength"] = "Maximum Length",
                ["Plugins.Shipping.Correios.Fields.MaximumLength.Hint"] = "Set the Maximum Length",
                ["Plugins.Shipping.Correios.Fields.MaximumHeight"] = "Maximum Height",
                ["Plugins.Shipping.Correios.Fields.MaximumHeight.Hint"] = "Set the Maximum Height",
                ["Plugins.Shipping.Correios.Fields.MaximumWidth"] = "Maximum Width",
                ["Plugins.Shipping.Correios.Fields.MaximumWidth.Hint"] = "Set the Maximum Width",
                ["Plugins.Shipping.Correios.Fields.MinimumWeight"] = "Minimum Weight",
                ["Plugins.Shipping.Correios.Fields.MinimumWeight.Hint"] = "Set the Minimum Weight",
                ["Plugins.Shipping.Correios.Fields.MaximumWeight"] = "Maximum Weight",
                ["Plugins.Shipping.Correios.Fields.MaximumWeight.Hint"] = "Set the Maximum Weight",

                ["Plugins.Shipping.Correios.Message.NoShipmentItems"] = "No shipment items",
                ["Plugins.Shipping.Correios.Message.AddressNotSet"] = "Shipping address is not set",
                ["Plugins.Shipping.Correios.Message.CountryNotSet"] = "Shipping country is not set",
                ["Plugins.Shipping.Correios.Message.StateNotSet"] = "Shipping state is not set",
                ["Plugins.Shipping.Correios.Message.PostalCodeNotSet"] = "Shipping zip postal code is not set",
                ["Plugins.Shipping.Correios.Message.DeliveryUninformed"] = "Delivery uninformed",
                ["Plugins.Shipping.Correios.Message.InvalidValueDelivery"] = "Invalid value delivery"
            });
            await base.InstallAsync();
        }

        public override async Task UninstallAsync()
        {
            await _settingService.DeleteSettingAsync<CorreiosSettings>();

            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.Url");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.Url.Hint");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.PostalCodeFrom");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.PostalCodeFrom.Hint");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.CompanyCode");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.CompanyCode.Hint");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.Password");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.Password.Hint");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.AddDaysForDelivery");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.AddDaysForDelivery.Hint");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.AvailableCarrierServices");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.AvailableCarrierServices.Hint");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.ServiceNameDefault");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.ServiceNameDefault.Hint");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.ShippingRateDefault");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.ShippingRateDefault.Hint");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.QtdDaysForDeliveryDefault");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.QtdDaysForDeliveryDefault.Hint");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.PercentageShippingFee");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.PercentageShippingFee.Hint");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.DeclaredMinimumValue");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.DeclaredMinimumValue.Hint");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.MinimumLength");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.MinimumLength.Hint");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.MinimumHeight");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.MinimumHeight.Hint");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.MinimumWidth");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.MinimumWidth.Hint");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.MaximumLength");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.MaximumLength.Hint");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.MaximumHeight");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.MaximumHeight.Hint");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.MaximumWidth");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.MaximumWidth.Hint");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.MinimumWeight");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.MinimumWeight.Hint");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.MaximumWeight");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Fields.MaximumWeight.Hint");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Message.NoShipmentItems");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Message.AddressNotSet");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Message.CountryNotSet");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Message.StateNotSet");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Message.PostalCodeNotSet");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Message.DeliveryUninformed");
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.Correios.Message.InvalidValueDelivery");

            await base.UninstallAsync();
        }

        //public ShippingRateComputationMethodType ShippingRateComputationMethodType => ShippingRateComputationMethodType.Realtime;

        public IShipmentTracker ShipmentTracker => new CorreiosShipmentTracker(_correiosSettings);


        //public bool HideShipmentMethods(IList<ShoppingCartItem> cart)
        //{
        //    if (cart == null || (cart != null && !cart.Any()))
        //    {
        //        return false;
        //    }
        //    var shippingItens = cart.Where(c => c.Product.IsShipEnabled).Select(c => c.Product).ToList();
        //    return _correiosService.DenyProductShipping(shippingItens);
        //}

        public async Task<GetShippingOptionResponse> GetShippingOptionsAsync(GetShippingOptionRequest getShippingOptionRequest)
        {
            if (getShippingOptionRequest == null)
                throw new ArgumentNullException("getShippingOptionRequest");

            var response = new GetShippingOptionResponse();

            if (!(await ValidateRequest(getShippingOptionRequest, response)))
                return response;

            try
            {
                WSCorreiosCalcPrecoPrazo.cResultado wsResult = _correiosService.RequestCorreios(getShippingOptionRequest);
                foreach (WSCorreiosCalcPrecoPrazo.cServico serv in wsResult?.Servicos)
                {
                    try
                    {
                        var obs = await ValidateWSResult(serv);
                        response.ShippingOptions.Add(GetShippingOption(ApplyAdditionalFee(Convert.ToDecimal(serv.Valor, new CultureInfo("pt-BR"))), CorreiosServiceType.GetServiceName(serv.Codigo.ToString()), CalcPrazoEntrega(serv), obs));
                    }
                    catch (Exception e)
                    {
                        await _logger.ErrorAsync(e.Message, e);
                    }
                }
            }
            catch (Exception e)
            {
                await _logger.ErrorAsync(e.Message, e);
            }

            if (response.ShippingOptions.Count <= 0 && _correiosSettings.ShippingRateDefault > 0 && _correiosSettings.QtdDaysForDeliveryDefault > 0)
            {
                // Default price
                response.ShippingOptions.Add(
                    GetShippingOption(_correiosSettings.ShippingRateDefault, _correiosSettings.ServiceNameDefault, _correiosSettings.QtdDaysForDeliveryDefault));
            }

            return response;
        }

        public Task<decimal?> GetFixedRateAsync(GetShippingOptionRequest getShippingOptionRequest)
        {
            throw new NotImplementedException();
        }
    }
}
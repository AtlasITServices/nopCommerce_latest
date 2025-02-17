﻿using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using NopBrasil.Plugin.Shipping.Correios.Domain;
using NopBrasil.Plugin.Shipping.Correios.Models;
using NopBrasil.Plugin.Shipping.Correios.Utils;
using System;
using System.Text;
using System.Threading.Tasks;

namespace NopBrasil.Plugin.Shipping.Correios.Controllers
{
    [Area(AreaNames.Admin)]
    public class ShippingCorreiosController : BasePluginController
    {
        private readonly CorreiosSettings _correiosSettings;
        private readonly INotificationService _notificationService;
        private readonly ISettingService _settingService;
        private readonly IWebHelper _webHelper;
        private readonly ILocalizationService _localizationService;
        private readonly ICategoryService _categoryService;
        private readonly IPermissionService _permissionService;

        public ShippingCorreiosController(CorreiosSettings correiosSettings, ISettingService settingService, 
            IWebHelper webHelper, ILocalizationService localizationService, ICategoryService categoryService,
            IPermissionService permissionService, INotificationService notificationService)
        {
            _correiosSettings = correiosSettings;
            _settingService = settingService;
            _webHelper = webHelper;
            _localizationService = localizationService;
            _categoryService = categoryService;
            _permissionService = permissionService;
            _notificationService = notificationService;
        }

        public async Task<IActionResult> Configure()
        {
            if (!(await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageShippingSettings)))
                return AccessDeniedView();

            var model = new CorreiosShippingModel
            {
                Url = _correiosSettings.Url,
                PostalCodeFrom = _correiosSettings.PostalCodeFrom,
                CompanyCode = _correiosSettings.CompanyCode,
                Password = _correiosSettings.Password,
                AddDaysForDelivery = _correiosSettings.AddDaysForDelivery.ToString(),
                ServiceNameDefault = _correiosSettings.ServiceNameDefault,
                ShippingRateDefault = _correiosSettings.ShippingRateDefault,
                QtdDaysForDeliveryDefault = _correiosSettings.QtdDaysForDeliveryDefault,
                PercentageShippingFee = _correiosSettings.PercentageShippingFee,
                DeclaredMinimumValue = _correiosSettings.DeclaredMinimumValue,
                MaximumHeight = _correiosSettings.MaximumHeight,
                MaximumLength = _correiosSettings.MaximumLength,
                MaximumWeight = _correiosSettings.MaximumWeight,
                MaximumWidth = _correiosSettings.MaximumWidth,
                MinimumHeight = _correiosSettings.MinimumHeight,
                MinimumLength = _correiosSettings.MinimumLength,
                MinimumWeight = _correiosSettings.MinimumWeight,
                MinimumWidth = _correiosSettings.MinimumWidth
            };

            foreach (string service in CorreiosServiceType.Services)
                model.AvailableCarrierServices.Add(service);

            LoadSavedServices(model);

            return View("~/Plugins/Shipping.Correios/Views/Configure.cshtml", model);
        }

        private void LoadSavedServices(CorreiosShippingModel model)
        {
            if (!string.IsNullOrEmpty(_correiosSettings.ServicesOffered))
                foreach (string service in CorreiosServiceType.Services)
                {
                    string serviceId = CorreiosServiceType.GetServiceId(service);
                    if (!string.IsNullOrEmpty(serviceId) && !string.IsNullOrEmpty(_correiosSettings.ServicesOffered))
                        if (_correiosSettings.ServicesOffered.Contains($"[{serviceId}]")) // Add delimiters [] so that single digit IDs aren't found in multi-digit IDs
                            model.ServicesOffered.Add(service);
                }
        }

        [HttpPost]
        public async Task<IActionResult> Configure(CorreiosShippingModel model)
        {
            if (!(await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageShippingSettings)))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return await Configure();

            _correiosSettings.Url = model.Url;
            _correiosSettings.PostalCodeFrom = model.PostalCodeFrom;
            _correiosSettings.CompanyCode = model.CompanyCode;
            _correiosSettings.Password = model.Password;
            _correiosSettings.AddDaysForDelivery = Convert.ToInt32(model.AddDaysForDelivery);
            _correiosSettings.ServiceNameDefault = model.ServiceNameDefault;
            _correiosSettings.ShippingRateDefault = model.ShippingRateDefault;
            _correiosSettings.QtdDaysForDeliveryDefault = model.QtdDaysForDeliveryDefault;
            _correiosSettings.PercentageShippingFee = model.PercentageShippingFee;

            _correiosSettings.MaximumHeight = model.MaximumHeight;
            _correiosSettings.MaximumLength = model.MaximumLength;
            _correiosSettings.MaximumWeight = model.MaximumWeight;
            _correiosSettings.MaximumWidth = model.MaximumWidth;
            _correiosSettings.MinimumHeight = model.MinimumHeight;
            _correiosSettings.MinimumLength = model.MinimumLength;
            _correiosSettings.MinimumWeight = model.MinimumWeight;
            _correiosSettings.MinimumWidth = model.MinimumWidth;
            _correiosSettings.DeclaredMinimumValue = model.DeclaredMinimumValue;

            _correiosSettings.ServicesOffered = GetSelectedServices(model);
            await _settingService.SaveSettingAsync(_correiosSettings);
            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));
            return await Configure();
        }

        private string GetSelectedServices(CorreiosShippingModel model)
        {
            var carrierServicesOfferedDomestic = new StringBuilder();
            if (model.CheckedCarrierServices != null)
                foreach (var cs in model.CheckedCarrierServices)
                {
                    string serviceId = CorreiosServiceType.GetServiceId(cs);
                    if (!string.IsNullOrEmpty(serviceId))
                        carrierServicesOfferedDomestic.Append($"[{serviceId}]:"); // Add delimiters [] so that single digit IDs aren't found in multi-digit IDs
                }
            return carrierServicesOfferedDomestic.ToString().RemoveLastIfEndsWith(":");
        }
    }
}
﻿// This file is part of AlarmWorkflow.
// 
// AlarmWorkflow is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// AlarmWorkflow is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with AlarmWorkflow.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using AlarmWorkflow.BackendService.AddressingContracts;
using AlarmWorkflow.BackendService.AddressingContracts.EntryObjects;
using AlarmWorkflow.BackendService.EngineContracts;
using AlarmWorkflow.BackendService.SettingsContracts;
using AlarmWorkflow.Shared.Core;
using AlarmWorkflow.Shared.Diagnostics;

namespace AlarmWorkflow.Job.SmsJob
{
    /// <summary>
    /// Implements a Job, that sends SMS with different sms services.
    /// </summary>
    [Export("SmsJob", typeof(IJob))]
    [Information(DisplayName = "ExportJobDisplayName", Description = "ExportJobDescription")]
    sealed class SmsJob : IJob
    {
        #region Fields

        private ISettingsServiceInternal _settings;
        private IAddressingServiceInternal _addressing;

        private string _userName;
        private string _password;
        private ISmsProvider _provider;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="SmsJob"/> class.
        /// </summary>
        public SmsJob()
        {
        }

        #endregion

        #region IJob Members

        void IJob.Execute(IJobContext context, Operation operation)
        {
            if (context.Phase != JobPhase.AfterOperationStored)
            {
                return;
            }

            IList<MobilePhoneEntryObject> recipients = GetRecipients(operation);
            if (recipients.Count == 0)
            {
                Logger.Instance.LogFormat(LogType.Info, this, Properties.Resources.NoRecipientsErrorMessage);
                return;
            }

            string format = _settings.GetSetting("SMSJob", "MessageFormat").GetValue<string>();
            string text = operation.ToString(format);
            text = text.Replace("Ö", "Oe").Replace("Ä", "Ae").Replace("Ü", "Ue").Replace("ö", "oe").Replace("ä", "ae").Replace("ü", "ue").Replace("ß", "ss");
            // Truncate the string if it is too long
            text = text.Truncate(160, true, true);

            // Invoke the provider-send asynchronous because it is a web request and may take a while
            _provider.Send(_userName, _password, recipients.Select(r => r.PhoneNumber), text);
        }

        private IList<MobilePhoneEntryObject> GetRecipients(Operation operation)
        {
            var recipients = _addressing.GetCustomObjectsFiltered<MobilePhoneEntryObject>(MobilePhoneEntryObject.TypeId, operation);
            return recipients.Select(ri => ri.Item2).ToList();
        }

        bool IJob.Initialize(IServiceProvider serviceProvider)
        {
            _settings = serviceProvider.GetService<ISettingsServiceInternal>();
            _addressing = serviceProvider.GetService<IAddressingServiceInternal>();

            _userName = _settings.GetSetting("SMSJob", "UserName").GetValue<string>();
            _password = _settings.GetSetting("SMSJob", "Password").GetValue<string>();
            _provider = ExportedTypeLibrary.Import<ISmsProvider>(_settings.GetSetting("SMSJob", "Provider").GetValue<string>());

            return true;
        }

        bool IJob.IsAsync
        {
            get { return true; }
        }

        #endregion

        #region IDisposable Members

        void System.IDisposable.Dispose()
        {

        }

        #endregion

    }
}

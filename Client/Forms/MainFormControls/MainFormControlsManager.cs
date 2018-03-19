﻿using BllEntities;
using Client.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Client.Forms.MainFormControls
{
    public class MainFormControlsManager
    {
        private readonly MainFormControls mainFormControls;
        AddEventForm addEventForm = null;

        public MainFormControlsManager(MainFormControls mainFormControls)
        {
            this.mainFormControls = mainFormControls;
            mainFormControls.DeleteEventButton.Click += удалитьСобытиеToolStripMenuItem_Click;
            mainFormControls.CreateEventButton.Click += создатьСобытиеToolStripMenuItem_Click;
            mainFormControls.SettingsButton.Click += настройкиToolStripMenuItem_Click;
            mainFormControls.SendOnEventButton.Click += переслатьСобытиеToolStripMenuItem_Click;
        }

        public void EnableSendOnEventButton()
        {
            mainFormControls.SendOnEventButton.Enabled = true;
        }

        public void DisableSendOnEventButton()
        {
            mainFormControls.SendOnEventButton.Enabled = false;
        }

        public void EnableDeleteEventButton()
        {
            mainFormControls.DeleteEventButton.Enabled = true;
        }

        public void DisableDeleteEventButton()
        {
            mainFormControls.DeleteEventButton.Enabled = false;
        }

        public void DisableCreateEventButton()
        {
            mainFormControls.CreateEventButton.Enabled = false;
        }

        public void EnableCreateEventButton()
        {
            mainFormControls.CreateEventButton.Enabled = true;
        }

        private void удалитьСобытиеToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (mainFormControls.ControllerSet.dataGridControlsManager.GetSelectedRowIndex() >= 0)
            {
                mainFormControls.ControllerSet.eventManager.RemoveEvent(mainFormControls.ControllerSet.dataGridControlsManager.GetSelectedRowIndex());
                if (!mainFormControls.ControllerSet.dataGridControlsManager.IsAnyRowSelected())
                {
                    DisableDeleteEventButton();
                    ClearDataControls();
                }
            }
        }

        public void ClearDataControls()
        {
            mainFormControls.ControllerSet.staticControlsManager.ClearControls();
            mainFormControls.ControllerSet.statusControlsManager.ClearControls();
            mainFormControls.ControllerSet.statusControlsManager.DisableStatusControls();
            DisableSendOnEventButton();
            mainFormControls.ControllerSet.fileControlsManager.ClearFileList();
            mainFormControls.ControllerSet.recieverControlsManager.UncheckCheckBox();
            mainFormControls.ControllerSet.recieverControlsManager.HideChecklistAndCheckbox();
            mainFormControls.DeleteEventButton.Enabled = false;
            mainFormControls.ControllerSet.noteControlsManager.DisableTextBox();
        }

        private void создатьСобытиеToolStripMenuItem_Click(object sender, EventArgs e)
        {
            mainFormControls.ControllerSet.client.PingServerAndIndicateHisStateOnControls();
            if (mainFormControls.ControllerSet.client.isServerOnline)
            {
                var createdEvent = GetNewEventUsingAddEventForm();
                if (createdEvent != null)
                {
                    mainFormControls.ControllerSet.eventManager.AddNewEventAndSerialize(createdEvent);
                    if (mainFormControls.ControllerSet.dataGridControlsManager.IsAnyRowSelected() == false)
                    {
                        mainFormControls.ControllerSet.dataGridControlsManager.ClearSelection();
                    }
                }
                addEventForm = null;
            }
        }

        private BllEvent GetNewEventUsingAddEventForm()
        {
            addEventForm = new AddEventForm(mainFormControls.ControllerSet.client.GetServerInstance(), mainFormControls.ControllerSet.client.GetUser());
            addEventForm.ShowDialog();
            return addEventForm.Event;
        }

        private void настройкиToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Settings settings = new Settings();
            bool prevHideClosed = AppConfigManager.GetBoolKeyValue(Properties.Resources.TAG_HIDE_CLOSED);
            int prevHideAllowance = AppConfigManager.GetIntKeyValue(Properties.Resources.TAG_HIDE_ALLOWANCE);
            settings.ShowDialog();
            if (prevHideClosed != AppConfigManager.GetBoolKeyValue(Properties.Resources.TAG_HIDE_CLOSED))
            {
                if (prevHideClosed)
                {
                    mainFormControls.ControllerSet.eventManager.ShowClosedEvents();
                }
                else
                {
                    mainFormControls.ControllerSet.eventManager.HideClosedEventsAccordingToConfigValue();
                }
            }
            else
            {
                if (prevHideClosed && (prevHideAllowance != AppConfigManager.GetIntKeyValue(Properties.Resources.TAG_HIDE_ALLOWANCE)))
                {
                    mainFormControls.ControllerSet.eventManager.HideClosedEventsAccordingToConfigValue();
                }
            }
        }

        private void переслатьСобытиеToolStripMenuItem_Click(object sender, EventArgs e)
        {
            mainFormControls.ControllerSet.client.PingServerAndIndicateHisStateOnControls();
            if (mainFormControls.ControllerSet.client.isServerOnline)
            {
                SendOnEventForm sendOnEventForm = new SendOnEventForm(mainFormControls.ControllerSet.client.GetServerInstance(), 
                    mainFormControls.ControllerSet.SelectedEvent.EventData, mainFormControls.ControllerSet.client.GetUser());
                sendOnEventForm.ShowDialog();
            }
        }
    }
}
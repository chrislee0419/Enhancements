﻿using HMUI;
using System;
using Zenject;
using UnityEngine;
using System.Linq;
using VRUIControls;
using IPA.Utilities;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.FloatingScreen;
using BeatSaberMarkupLanguage.ViewControllers;
using BeatSaberMarkupLanguage.Components.Settings;
using UnityEngine.SceneManagement;

namespace Enhancements.Timers
{
    [ViewDefinition("Enhancements.Views.Timers.notification-view.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\Timers\notification-view.bsml")]
    public class NotificationView : BSMLAutomaticViewController
    {
        private string _startScene;
        private Notifier _notifier;
        private FloatingScreen _floatingScreen;
        private ITimerController _timerController;
        private ITimeNotification _currentNotification;
        private PhysicsRaycasterWithCache _physicsRaycasterWithCache;

        [UIParams]
        protected BSMLParserParams parserParams;

        [UIComponent("dropdown")]
        protected DropDownListSetting dropdownSetting;

        [UIComponent("snooze-modal")]
        protected RectTransform snoozeModal;

        private int _length = 5;
        [UIValue("length")]
        protected int Length
        {
            get => _length;
            set
            {
                _length = value;
                parserParams.EmitEvent("get-units");
            }
        }

        private string _title = "Notification";
        [UIValue("title")]
        protected string Title
        {
            get => _title;
            set
            {
                _title = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue("unit-options")]
        public List<object> unitOptions;

        [UIValue("unit")]
        protected TimeType Unit { get; set; } = TimeType.Minutes;

        public bool Visible
        {
            get => _floatingScreen.gameObject.activeInHierarchy;
            set
            {
                _notifier.IsViewing = value;
                _floatingScreen.SetRootViewController(value ? this : null, value ? AnimationType.In : AnimationType.Out);
            }
        }

        [Inject]
        public void Construct(ITimerController controller, Notifier notifier, PhysicsRaycasterWithCache physicsRaycasterWithCache)
        {
            _physicsRaycasterWithCache = physicsRaycasterWithCache;
            _startScene = gameObject.scene.name;
            _notifier = notifier;
            _timerController = controller;
            unitOptions = new List<object>();
            unitOptions.AddRange(new TimeType[]
            {
                TimeType.Seconds,
                TimeType.Minutes,
                TimeType.Hours,
            }.Select(x => x as object));
            _ = CreateScreen();
            _notifier.NotificationPing += ShowNotification;
            SceneManager.activeSceneChanged += SceneChanged;
        }

        private void SceneChanged(Scene oldScene, Scene newScene)
        {
            if (_startScene == newScene.name)
                Catch();
        }

        [UIAction("#post-parse")]
        protected void Parsed()
        {
            var modalGO = dropdownSetting.dropdown.GetField<ModalView, DropdownWithTableView>("_modalView").gameObject;
            modalGO.transform.localPosition = new Vector3(modalGO.transform.localPosition.x, modalGO.transform.localPosition.y, -5f);
            snoozeModal.transform.localPosition = new Vector3(snoozeModal.transform.localPosition.x, snoozeModal.transform.localPosition.y, -10f);
        }

        protected void OnEnable()
        {
            if (!(_notifier is null))
            {
                _notifier.NotificationPing += ShowNotification;
                Catch();
            }
        }

        protected void OnDisable()
        {
            if (!(_notifier is null))
            {
                _notifier.NotificationPing -= ShowNotification;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _notifier.NotificationPing -= ShowNotification;
            SceneManager.activeSceneChanged -= SceneChanged;
        }

        public void ShowNotification(ITimeNotification notification)
        {
            if (!(notification is null))
            {
                _currentNotification = notification;
                Title = _currentNotification.Text;
                Visible = true;
            }
        }

        private async Task CreateScreen()
        {
            _floatingScreen = FloatingScreen.CreateFloatingScreen(new Vector2(130f, 70f), false, new Vector3(0f, 3.5f, 2.1f), Quaternion.Euler(new Vector3(325f, 0f, 0f)));
            _floatingScreen.GetComponent<VRGraphicRaycaster>().SetField("_physicsRaycaster", _physicsRaycasterWithCache);
            Visible = true;
            await SiraUtil.Utilities.PauseChamp;
            Visible = false;
        }

        [UIAction("format-units")]
        protected string FormatUnits(TimeType timeType)
        {
            return Length == 1 ? timeType.ToString().TrimEnd('s') : timeType.ToString();
        }

        protected void Reset()
        {
            Length = 5;
            Unit = default;
            parserParams.EmitEvent("get");
            parserParams.EmitEvent("get-units");
        }

        [UIAction("cancel")]
        protected void Cancel()
        {
            Reset();
            parserParams.EmitEvent("hide-modal");
        }

        [UIAction("confirm")]
        protected void Confirm()
        {
            Create();
            parserParams.EmitEvent("hide-modal");
            Reset();
        }

        protected void Create()
        {
            DateTime time = DateTime.Now;
            switch (Unit)
            {
                case TimeType.Minutes:
                    time = time.AddMinutes(Length);
                    break;
                case TimeType.Seconds:
                    time = time.AddSeconds(Length);
                    break;
                case TimeType.Hours:
                    time = time.AddHours(Length);
                    break;
            }
            _timerController.RegisterNotification(new GenericNotification(_currentNotification.Text, time));
            Visible = false;
        }

        [UIAction("dismiss")]
        protected void Dismiss()
        {
            Visible = false;
            Catch();
        }

        protected async void Catch()
        {
            await Task.Run(() => Thread.Sleep(500));
            var nextNotif = _notifier.NextNotification();
            if (!(nextNotif is null))
            {
                ShowNotification(nextNotif);
            }
        }
    }
}
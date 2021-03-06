﻿using System;
using System.Collections.Generic;
using System.Linq;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Bluetooth;
using Android.Support.V4.App;
using RadiusNetworks.IBeaconAndroid;

namespace TagBeacons
{
    [Activity(Label = "TagBeacons", MainLauncher = true, LaunchMode = Android.Content.PM.LaunchMode.SingleTask)]
    public class MainActivity : Activity, IBeaconConsumer
    {
        private const string TagName = "TagPoints";
        private bool AppPaused;
        private IBeaconManager beaconManager;
        private MonitorNotifier monitorNotifier;
        private RangeNotifier rangeNotifier;
        private Region monitorRegion, rangeRegion;
        private TextView textUUID, textProximity, textDistance, textMajor, textMinor, textMessage, textNumberSeen, textStatus, textInRange;
        private DBManager dbm;

        public MainActivity()
        {
            beaconManager = IBeaconManager.GetInstanceForApplication(this);
            monitorNotifier = new MonitorNotifier();
            rangeNotifier = new RangeNotifier();
            monitorRegion = new Region(TagName, null, null, null);
            rangeRegion = new Region(TagName, null, null, null);
            dbm = new DBManager();
            dbm.SetupDB();
        }

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            SetContentView(Resource.Layout.Main);
            textUUID = FindViewById<TextView>(Resource.Id.textUUID);
            textProximity = FindViewById<TextView>(Resource.Id.textProximity);
            textDistance = FindViewById<TextView>(Resource.Id.textDistance);
            textMajor = FindViewById<TextView>(Resource.Id.textMajor);
            textMinor = FindViewById<TextView>(Resource.Id.textMinor);
            textMessage = FindViewById<TextView>(Resource.Id.textMessage);
            textNumberSeen = FindViewById<TextView>(Resource.Id.textNumberSeen);
            textStatus = FindViewById<TextView>(Resource.Id.textStatus);
            textInRange = FindViewById<TextView>(Resource.Id.textInRange);

            textUUID.Text = textProximity.Text = textMajor.Text = textMinor.Text = textMessage.Text = "";

            var btAdapter = BluetoothAdapter.DefaultAdapter;
            if (btAdapter.IsEnabled)
            {
                textStatus.SetTextColor(Android.Graphics.Color.AliceBlue);
                textStatus.Text = "Searching";
            }
            else
            {
                textStatus.SetTextColor(Android.Graphics.Color.Red);
                textStatus.Text = "Bluetooth not enabled";
            }

            beaconManager.Bind(this);

            monitorNotifier.EnterRegionComplete += EnterRegion;
            monitorNotifier.ExitRegionComplete += ExitRegion;
            rangeNotifier.DidRangeBeaconsInRegionComplete += RangingBeaconsInRegion;
        }

        protected override void OnResume()
        {
            base.OnResume();
            AppPaused = false;
        }

        protected override void OnPause()
        {
            base.OnPause();
            AppPaused = true;
        }

        public void OnIBeaconServiceConnect()
        {
            try
            {
                beaconManager.SetMonitorNotifier(monitorNotifier);
                beaconManager.SetRangeNotifier(rangeNotifier);
                beaconManager.StartMonitoringBeaconsInRegion(monitorRegion);
                beaconManager.StartRangingBeaconsInRegion(rangeRegion);
            }
            catch (RemoteException ex)
            {
                Console.WriteLine("Exception in connection - {0}", ex.Message);
            }
        }

        private void EnterRegion(object s, MonitorEventArgs e)
        {
            RunOnUiThread(() =>
            {
                textStatus.SetTextColor(Android.Graphics.Color.Green);
                textStatus.Text = "Beacon detected";
            });
        }

        private void ExitRegion(object s, MonitorEventArgs e)
        {
            RunOnUiThread(() =>
            {
                textStatus.SetTextColor(Android.Graphics.Color.Blue);
                textStatus.Text = "Searching";
            });
        }

        private void RangingBeaconsInRegion(object s, RangeEventArgs e)
        {
            RunOnUiThread(() => textInRange.Text = e.TagBeacons.Count.ToString());
            var beac = e.TagBeacons.FirstOrDefault();
            if (beac != null)
            {
                string prox = "";
                RunOnUiThread(() => textUUID.Text = beac.ProximityUuid);
                switch ((ProximityType)beac.Proximity)
                {
                    case ProximityType.Immediate:
                        prox = "Very close";
                        break;
                    case ProximityType.Near:
                        prox = "Close";
                        break;
                    case ProximityType.Far:
                        prox = "Not too close";
                        break;
                }
                RunOnUiThread(() =>
                {
                    textProximity.Text = prox;
                    textMajor.Text = beac.Major.ToString();
                    textMinor.Text = beac.Minor.ToString();
                    textDistance.Text = beac.Accuracy.ToString();
                });
                var exist = dbm.GetSingleBeacon(beac.ProximityUuid);

                var beacon = new Beacon
                {
                    Proximity = beac.Proximity,
                    DeviceUUID = beac.ProximityUuid,
                    SignalStrength = beac.Rssi,
                    Major = beac.Major,
                    Minor = beac.Minor,
                    DeviceDistance = beac.Accuracy,
                    WelcomeMessage = "",
                    LastSeen = DateTime.Now,
                };

                if (exist == null)
                    beacon.FirstSeen = DateTime.Now;

                dbm.InsertOrUpdateBeacon(beacon);
                var total = dbm.GetListOfBeacons().Select(t => t.DeviceUUID).Distinct().ToList().Count;
                RunOnUiThread(() => textNumberSeen.Text = total.ToString());
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            monitorNotifier.EnterRegionComplete -= EnterRegion;
            monitorNotifier.ExitRegionComplete -= ExitRegion;
            rangeNotifier.DidRangeBeaconsInRegionComplete -= RangingBeaconsInRegion;
            beaconManager.StopMonitoringBeaconsInRegion(monitorRegion);
            beaconManager.StopRangingBeaconsInRegion(rangeRegion);
            beaconManager.UnBind(this);
        }
    }
}



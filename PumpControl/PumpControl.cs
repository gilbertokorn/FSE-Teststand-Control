using MccDaq;
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading;

namespace PumpControl
{
    public partial class PumpControl : Form
    {
        CancellationTokenSource cts;
        public int BoardNumber { get; set; }
        MccBoard DaqBoard;
        const string OFF = "OFF";   
        const string ON = "ON";
        const int PumpControlBit = 0, 
            Valve1Control = 1,
            Valve2Control = 2,
            Valve3Control = 3,
            Valve4Control = 4,
            Valve5Control = 5,
            Valve6Control = 6,
            Valve7Control = 7;

        const int TestValve = 1,
            DrainTankValve = 2,
            Tank1Valve = 3,
            Tank2Valve = 4,
            N2Valve = 5,
            DIWValve = 6,
            TankSwitchValve = 7;

        const int pumpInitializationTime = 5000;
        const int DICleaningTime = 10000;
        const int drainTime = 7000;
        const int tankSetTime = 3000;
        const int N2CleaningTime = 5000;
        const int shortFlushTime = 5000;
        const int N2DISwitchDelay = 1000;

        //INITIALIZE
        public PumpControl()
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            int boardNumber = 0;
            MccBoard myBoard;

            myBoard = new MccBoard(boardNumber);

            //Pump
            myBoard.DConfigBit(DigitalPortType.AuxPort, PumpControlBit, DigitalPortDirection.DigitalOut);

            //Valves
            myBoard.DConfigBit(DigitalPortType.AuxPort, Valve1Control, DigitalPortDirection.DigitalOut);
            myBoard.DConfigBit(DigitalPortType.AuxPort, Valve2Control, DigitalPortDirection.DigitalOut);
            myBoard.DConfigBit(DigitalPortType.AuxPort, Valve3Control, DigitalPortDirection.DigitalOut);
            myBoard.DConfigBit(DigitalPortType.AuxPort, Valve4Control, DigitalPortDirection.DigitalOut);
            myBoard.DConfigBit(DigitalPortType.AuxPort, Valve5Control, DigitalPortDirection.DigitalOut);
            myBoard.DConfigBit(DigitalPortType.AuxPort, Valve6Control, DigitalPortDirection.DigitalOut);
            myBoard.DConfigBit(DigitalPortType.AuxPort, Valve7Control, DigitalPortDirection.DigitalOut);

            DaqBoard = myBoard;
            BoardNumber = boardNumber;
            InitializeComponent();
            myBoard.DioConfig.GetDevType(1, out int configVal);
        }

        private void PumpControl_Load(object sender, EventArgs e)
        {
            //Saftey Off
            StopPump();
            ValveControlOff(new List<int>() { TestValve, DrainTankValve, Tank1Valve, Tank2Valve, N2Valve, DIWValve, TankSwitchValve });
            InitializeComboBox();
          
            TestStatus.Text = OFF;
            ErrorMessage.Text = "";
            TestStatus.ForeColor = Color.Red;
        }

        private void InitializeComboBox()
        {
            string[] tankNames = new string[] { "Tank1", "Tank2" };

            List<ComboBox> dropdownList = new List<ComboBox>() { ReFlowTestSourceDropdown, ReFlowTestDrainDropdown };

            foreach (ComboBox item in dropdownList)
            {
                item.Items.AddRange(tankNames);
                item.SelectedIndex = 0;
            }
            ReFlowTestDrainDropdown.Items.Add("Drain");
        }

        //SAFETY
        private void StopSystem()
        {
            StopPump();
            ValveControlOff(new List<int>() { TestValve, DrainTankValve, Tank1Valve, Tank2Valve, N2Valve, DIWValve, TankSwitchValve });
            HandleUIElements(false);
        }

        private void StopProcessButton_Click(object sender, EventArgs e)
        {
            StopSystem();
        }

        private void SafetyStopSystem(object sender, FormClosedEventArgs e)
        {
            StopSystem();
        }

        private async void CleanButton_Click(object sender, EventArgs e)
        {
            HandleUIElements(true);

            //Stop Etch and DIW/N2 flush
            StopPump();
            TestStatus.Text = "DI Water flush";
            TestStatus.ForeColor = Color.Green;
            ValveControlOff(new List<int>() { TestValve, DrainTankValve, Tank1Valve, Tank2Valve, N2Valve });
            ValveControlOn(new List<int>() { DIWValve });
            await Task.Delay(DICleaningTime);

            //N2 flush
            TestStatus.Text = "N2 flush";
            ValveControlOff(new List<int>() { TestValve, DrainTankValve, Tank1Valve, Tank2Valve, DIWValve });
            await Task.Delay(N2DISwitchDelay);
            ValveControlOn(new List<int>() { N2Valve });
            await Task.Delay(N2CleaningTime);

            //Short DIW flush
            TestStatus.Text = "DI Water flush";
            TestStatus.ForeColor = Color.Green;
            ValveControlOff(new List<int>() { TestValve, DrainTankValve, Tank1Valve, Tank2Valve, N2Valve });
            await Task.Delay(N2DISwitchDelay);
            ValveControlOn(new List<int>() { DIWValve });
            await Task.Delay(shortFlushTime);

            //short N2 flush
            TestStatus.Text = "N2 flush";
            ValveControlOff(new List<int>() { TestValve, DrainTankValve, Tank1Valve, Tank2Valve, DIWValve });
            await Task.Delay(N2DISwitchDelay);
            ValveControlOn(new List<int>() { N2Valve });
            await Task.Delay(shortFlushTime);

            //Stop Cleaning and Pump
            ValveControlOff(new List<int>() { N2Valve, DIWValve });
            StopPump();

            HandleUIElements(false);
        }

        //PUMP CONTROL
        private void StartPump_Click(object sender, EventArgs e)
        {
            ErrorInfo info = StartPump();
            HandleErrorMessage(info);
        }

        private async void Flowrate_Click(object sender, EventArgs e)
        {
            StartPump();
            await Task.Delay(60000);
            StopPump();
        }

        private void StopPump_Click(object sender, EventArgs e)
        {
            ErrorInfo info = StopPump();
            HandleErrorMessage(info);
        }

        private ErrorInfo StartPump()
        {
            PumpLabel.ForeColor = Color.Green;
            ErrorInfo info = DaqBoard.DBitOut(DigitalPortType.AuxPort, PumpControlBit, DigitalLogicState.High);
            return info;
        }

        private ErrorInfo StopPump()
        {
            PumpLabel.ForeColor = Color.Red;
            ErrorInfo info = DaqBoard.DBitOut(DigitalPortType.AuxPort, PumpControlBit, DigitalLogicState.Low);
            return info;
        }

        private void HandleErrorMessage(ErrorInfo info)
        {
            if (info.Value != ErrorInfo.ErrorCode.NoErrors)
            {
                ErrorMessage.Text = info.Message;
            }
        }

        //VALVE CONTROL
        public void StartValve_Click(object sender, EventArgs e)
        {
            string regexString = "[1-9]";
            string valveButtonName = sender.GetType().GetProperty("Name").GetValue(sender, null).ToString();
            var valveNumberRegexed = Regex.Match(valveButtonName, regexString).ToString();
            var valveNumber = int.Parse(valveNumberRegexed);

            ValveControlOn(new List<int>() { valveNumber });
        }

        public void StopValve_Click(object sender, EventArgs e)
        {
            string regexString = "[1-9]";
            string valveButtonName = sender.GetType().GetProperty("Name").GetValue(sender, null).ToString();
            var valveNumberRegexed = Regex.Match(valveButtonName, regexString).ToString();
            var valveNumber = int.Parse(valveNumberRegexed);

            ValveControlOff(new List<int>() { valveNumber });
        }

        private void ValveControlOn (List<int> valveNumbers)
        {
            foreach(var valveNumber in valveNumbers)
            {
                ErrorInfo info = DaqBoard.DBitOut(DigitalPortType.AuxPort, valveNumber, DigitalLogicState.Low);
                string valveLabelName = "Valve" + valveNumber + "Label";

                var control = Controls.OfType<Label>()
                           .FirstOrDefault(c => c.Name == valveLabelName);

                if (control != null)
                {
                    control.Invoke((MethodInvoker)(() => control.ForeColor = Color.Green));
                }
            };
        }

        private void ValveControlOff(List<int> valveNumbers)
        {
            foreach (var valveNumber in valveNumbers)
            {
                ErrorInfo info = DaqBoard.DBitOut(DigitalPortType.AuxPort, valveNumber, DigitalLogicState.High);
                string valveLabelName = "Valve" + valveNumber + "Label";

                var control = Controls.OfType<Label>()
                           .FirstOrDefault(c => c.Name == valveLabelName);

                if (control != null)
                {
                    control.Invoke((MethodInvoker)(() => control.ForeColor = Color.Red));
                }
            };
        }

        //TANK AND DRAIN CONTROL
        private int DrainSelection()
        {
            int drainSelected = -1;
            if (ReFlowTestDrainDropdown.SelectedIndex == 0)
            {
                drainSelected = Tank1Valve;
            }
            else if (ReFlowTestDrainDropdown.SelectedIndex == 1)
            {
                drainSelected = Tank2Valve;
            }
            else if (ReFlowTestDrainDropdown.SelectedIndex == 2)
            {
                drainSelected = -1;
            }
            return drainSelected;
        }

        private void TankSelection()
        {
            if (ReFlowTestSourceDropdown.SelectedIndex == 0)
            {
                ValveControlOff(new List<int>() { TankSwitchValve });
            }
            else if (ReFlowTestSourceDropdown.SelectedIndex == 1)
            {
                ValveControlOn(new List<int>() { TankSwitchValve });
            }
            else
            {
                return;
            };
        }

        //PROCESS AND TEST CONTROL
        private async void RunReFlowTest(int etchWaitTime)
        {
            cts = new CancellationTokenSource();

            //Set Source Tank to Tank1 or Tank2 (depending on dropdown selected)
            TankSelection();

            //Check if "drain" is set to Tank1, Tank2 or Drain based on dropdown selected
            int drainSelected = DrainSelection();

            //Wait for Tank and Drain to be set
            await Task.Delay(tankSetTime);

            //Initialize Test
            StartPump();
            TestStatus.Text = "Pump started";
            await Task.Delay(pumpInitializationTime);

            //TEST TIME
            TestStatus.Text = "Etch Process started";
            ValveControlOn(new List<int>() { TestValve }); //First run to drain to remove remaining water in system
             //time required to remove water from system via drain before going to tank

            //if the set etchtime is less than the time required to 
            //remove cleaning water, then process wont go to tank
            if (etchWaitTime > drainTime)
            {
                Console.WriteLine(drainSelected);
                await Task.Delay(drainTime);
                if (drainSelected != -1)
                {
                    ValveControlOn(new List<int>() { DrainTankValve, drainSelected }); //Now run process into selected tank.   
                }
                await Task.Delay(etchWaitTime - drainTime);
            } else if (etchWaitTime <= drainTime)
            {
                await Task.Delay(etchWaitTime);
            }

            //Stop Etch and DIW/N2 flush
            TestStatus.Text = "DI Water flush";
            TestStatus.ForeColor = Color.Green;
            ValveControlOff(new List<int>() { TestValve, DrainTankValve, Tank1Valve, Tank2Valve, N2Valve });
            ValveControlOn(new List<int>() { DIWValve });
            await Task.Delay(DICleaningTime);

            //N2 flush
            TestStatus.Text = "N2 flush";
            ValveControlOff(new List<int>() { TestValve, DrainTankValve, Tank1Valve, Tank2Valve, DIWValve });
            ValveControlOn(new List<int>() { N2Valve });
            await Task.Delay(N2CleaningTime);

            //Short DIW flush
            TestStatus.Text = "DI Water flush";
            TestStatus.ForeColor = Color.Green;
            ValveControlOff(new List<int>() { TestValve, DrainTankValve, Tank1Valve, Tank2Valve, N2Valve });
            ValveControlOn(new List<int>() { DIWValve });
            await Task.Delay(shortFlushTime);

            //short N2 flush
            TestStatus.Text = "N2 flush";
            ValveControlOff(new List<int>() { TestValve, DrainTankValve, Tank1Valve, Tank2Valve, DIWValve });
            ValveControlOn(new List<int>() { N2Valve });
            await Task.Delay(shortFlushTime);

            //Stop Cleaning and Pump
            ValveControlOff(new List<int>() { N2Valve, DIWValve });
            StopPump();

            HandleUIElements(false);
            cts = null;
        }

        //Test button click events
        private void RunReFlowTestButton_Click(object sender, EventArgs e)
        {
            var errorEtchWaitTime = -1;
            var etchWaitTime = HandleUIElements(true);
            if (etchWaitTime == errorEtchWaitTime)
            {
                TestStatus.Text = "Etchtime set is too low";
                TestStatus.ForeColor = Color.Red;
            }
            else RunReFlowTest(etchWaitTime);
        }

        //Enable/Disable Dropdowns and Input Fields and Set UI colors/states
        private int HandleUIElements(bool testState)
        {
            TestStatus.Text = "Initializing";
            TestStatus.ForeColor = Color.Red; 

            int etchWaitTime;
            if (SetEtchTime.Text == "" || int.Parse(SetEtchTime.Text) < 1)
            {
                return -1;
            }
            else
            {
                etchWaitTime = int.Parse(SetEtchTime.Text) * 1000;
            }

            if (testState == true)
            {
                SetEtchTime.ReadOnly = true;
                ReFlowTestSourceDropdown.Enabled = false;
                ReFlowTestDrainDropdown.Enabled = false;
                TestStatus.ForeColor = Color.Green;
            }
            else if (testState == false)
            {
                TestStatus.Text = OFF;
                TestStatus.ForeColor = Color.Red;
                SetEtchTime.ReadOnly = false;
                ReFlowTestSourceDropdown.Enabled = true;
                ReFlowTestDrainDropdown.Enabled = true;
            }

            return etchWaitTime;
        }
    }
}

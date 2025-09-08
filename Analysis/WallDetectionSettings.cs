using System;
using System.Drawing;
using System.Windows.Forms;

namespace LoadPCDtest.Analysis
{
	public class WallDetectionSettings : Form
	{
		public class Values
		{
			public float InitialWidthMeters { get; set; }
			public float StepMeters { get; set; }
			public int MinBaselinePoints { get; set; }
			public bool ChooseAfterDrop { get; set; }
			public float OutwardBiasMeters { get; set; }
			public bool UseRatioThreshold { get; set; }
			public float RatioThresholdPercent { get; set; }
		}

		public static Values Defaults { get; } = new Values
		{
			InitialWidthMeters = 2.0f,
			StepMeters = 0.05f,
			MinBaselinePoints = 50,
			ChooseAfterDrop = true,
			OutwardBiasMeters = 0.05f,
			UseRatioThreshold = true,
			RatioThresholdPercent = 60f
		};

		private NumericUpDown numWidth;
		private NumericUpDown numStep;
		private NumericUpDown numBaseline;
		private CheckBox chkAfterDrop;
		private NumericUpDown numBias;
		private CheckBox chkUseRatio;
		private NumericUpDown numRatio;
		private Button btnOk;
		private Button btnCancel;

		public Values Result { get; private set; }

		public WallDetectionSettings()
		{
			Text = "墙面检测参数";
			FormBorderStyle = FormBorderStyle.FixedDialog;
			StartPosition = FormStartPosition.CenterParent;
			MaximizeBox = false;
			MinimizeBox = false;
			ClientSize = new Size(360, 260);

			var lblWidth = new Label { Text = "初始宽度 (m):", Left = 12, Top = 16, Width = 130 };
			numWidth = new NumericUpDown { Left = 160, Top = 12, Width = 160, DecimalPlaces = 2, Minimum = 0.10M, Maximum = 50M, Increment = 0.10M };
			numWidth.Value = (decimal)Defaults.InitialWidthMeters;

			var lblStep = new Label { Text = "步长 (m):", Left = 12, Top = 46, Width = 130 };
			numStep = new NumericUpDown { Left = 160, Top = 42, Width = 160, DecimalPlaces = 3, Minimum = 0.01M, Maximum = 2M, Increment = 0.01M };
			numStep.Value = (decimal)Defaults.StepMeters;

			var lblBaseline = new Label { Text = "最少基线点数:", Left = 12, Top = 76, Width = 130 };
			numBaseline = new NumericUpDown { Left = 160, Top = 72, Width = 160, Minimum = 1, Maximum = 100000, Increment = 10 };
			numBaseline.Value = Defaults.MinBaselinePoints;

			chkAfterDrop = new CheckBox { Text = "选择下降后宽度(更贴外侧)", Left = 12, Top = 102, Width = 308 };
			chkAfterDrop.Checked = Defaults.ChooseAfterDrop;

			var lblBias = new Label { Text = "外推偏置 (m):", Left = 12, Top = 130, Width = 130 };
			numBias = new NumericUpDown { Left = 160, Top = 126, Width = 160, DecimalPlaces = 2, Minimum = 0M, Maximum = 2M, Increment = 0.01M };
			numBias.Value = (decimal)Defaults.OutwardBiasMeters;

			chkUseRatio = new CheckBox { Text = "按占比阈值选择(%)", Left = 12, Top = 156, Width = 150 };
			chkUseRatio.Checked = Defaults.UseRatioThreshold;
			numRatio = new NumericUpDown { Left = 160, Top = 154, Width = 160, DecimalPlaces = 0, Minimum = 1, Maximum = 100, Increment = 1 };
			numRatio.Value = (decimal)Defaults.RatioThresholdPercent;

			btnOk = new Button { Text = "确定", Left = 160, Width = 70, Top = 200, DialogResult = DialogResult.OK };
			btnCancel = new Button { Text = "取消", Left = 250, Width = 70, Top = 200, DialogResult = DialogResult.Cancel };

			Controls.AddRange(new Control[] { lblWidth, numWidth, lblStep, numStep, lblBaseline, numBaseline, chkAfterDrop, lblBias, numBias, chkUseRatio, numRatio, btnOk, btnCancel });

			AcceptButton = btnOk;
			CancelButton = btnCancel;

			btnOk.Click += (s, e) =>
			{
				Result = new Values
				{
					InitialWidthMeters = (float)numWidth.Value,
					StepMeters = (float)numStep.Value,
					MinBaselinePoints = (int)numBaseline.Value,
					ChooseAfterDrop = chkAfterDrop.Checked,
					OutwardBiasMeters = (float)numBias.Value,
					UseRatioThreshold = chkUseRatio.Checked,
					RatioThresholdPercent = (float)numRatio.Value
				};
			};
		}

		public static bool TryGetValues(IWin32Window owner, out Values values)
		{
			using (var dlg = new WallDetectionSettings())
			{
				var res = dlg.ShowDialog(owner);
				values = res == DialogResult.OK ? dlg.Result : null;
				if (values != null)
				{
					Defaults.InitialWidthMeters = values.InitialWidthMeters;
					Defaults.StepMeters = values.StepMeters;
					Defaults.MinBaselinePoints = values.MinBaselinePoints;
					Defaults.ChooseAfterDrop = values.ChooseAfterDrop;
					Defaults.OutwardBiasMeters = values.OutwardBiasMeters;
					Defaults.UseRatioThreshold = values.UseRatioThreshold;
					Defaults.RatioThresholdPercent = values.RatioThresholdPercent;
				}
				return res == DialogResult.OK;
			}
		}
	}
}



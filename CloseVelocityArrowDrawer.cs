using System;
using SFS.UI;
using SFS.World.Maps;
using UnityEngine;
using UnityEngine.UI;
using HarmonyLib;

//namespace SFS.World;

public class CloseVelocityArrowDrawer : MonoBehaviour
{
	[Serializable]
	public class Arrow
	{
		public RectTransform holder;

		public RectTransform holder_Shadow;

		public Image line;

		public Image line_Shadow;

		public Text text;

		public Text text_Shadow;

		public void Position(string text, float lineLength, Vector2 position, Vector2 directionNormal)
		{
			SetActive(active: true);
			holder.position = position;
			holder_Shadow.localPosition = (Vector2)holder.localPosition + new Vector2(2f, -2f);
			Quaternion quaternion3 = (holder.localRotation = (holder_Shadow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(directionNormal.y, directionNormal.x) * 57.29578f)));
			Vector2 vector3 = (line.rectTransform.sizeDelta = (line_Shadow.rectTransform.sizeDelta = new Vector2(lineLength, line.rectTransform.sizeDelta.y)));
			string text4 = (this.text.text = (text_Shadow.text = text));
			quaternion3 = (this.text.rectTransform.rotation = (text_Shadow.rectTransform.rotation = Quaternion.identity));
			Vector3 vector6 = (this.text.rectTransform.localScale = (text_Shadow.rectTransform.localScale = Vector3.one * 0.25f));
		}

		public void SetActive(bool active)
		{
			if (holder.gameObject.activeSelf != active)
			{
				holder.gameObject.SetActive(active);
				holder_Shadow.gameObject.SetActive(active);
			}
		}
	}

	public RectTransform safeArea;

	public Arrow velocity_X;

	public Arrow velocity_Y;

	private void Start()
	{
		SFS.World.WorldView main = SFS.World.WorldView.main;
		main.onViewLocationChange_After = (Action<SFS.World.Location, SFS.World.Location>)Delegate.Combine(main.onViewLocationChange_After, new Action<SFS.World.Location, SFS.World.Location>(OnLocationChange));
		SFS.World.PlayerController.main.player.OnChange += new Action(OnPlayerChange);
	}

	private void OnDestroy()
	{
		SFS.World.PlayerController.main.player.OnChange -= new Action(OnPlayerChange);
	}

	private void OnPlayerChange()
	{
		if (!(SFS.World.PlayerController.main.player.Value is SFS.World.Rocket))
		{
			velocity_X.SetActive(active: false);
			velocity_Y.SetActive(active: false);
		}
	}

	private void OnLocationChange(SFS.World.Location old, SFS.World.Location location)
	{
		FileLog.Log("OnLocationChange called");

		if (!(SFS.World.PlayerController.main.player.Value is SFS.World.Rocket) || (bool)Map.manager.mapMode)
		{
			velocity_X.SetActive(active: false);
			velocity_Y.SetActive(active: false);
			return;
		}

		float sizeRadius = SFS.World.PlayerController.main.player.Value.GetSizeRadius();
		if ((float)SFS.World.WorldView.main.viewDistance > sizeRadius * 50f + 50f)
		{
			velocity_X.SetActive(active: false);
			velocity_Y.SetActive(active: false);
			return;
		}

		//bool flag = !double.IsNaN(location.planet.data.basics.velocityArrowsHeight);
		//double num = (flag ? location.planet.data.basics.velocityArrowsHeight : location.planet.data.basics.timewarpHeight);
		//bool num2 = location.planet.codeName != SFS.Base.planetLoader.spaceCenter.address && (flag ? location.TerrainHeight : location.Height) < num;
		//float num3 = sizeRadius * (SFS.World.GameCamerasManager.main.world_Camera.camera.WorldToScreenPoint(Vector3.zero) - SFS.World.GameCamerasManager.main.world_Camera.camera.WorldToScreenPoint(Vector3.right)).magnitude;
		Vector2 origin = SFS.World.GameCamerasManager.main.world_Camera.camera.WorldToScreenPoint(SFS.World.WorldView.ToLocalPosition(location.position));
		/*if (num2)
		{
			Vector2 vector = location.velocity.Rotate(0f - SFS.World.GameCamerasManager.main.world_Camera.CameraRotationRadians);
			float num4 = GetArrowLength(Mathf.Abs(vector.x)) * 6f;
			float num5 = GetArrowLength(Mathf.Abs(vector.y)) * 6f;
			if (num4 > 0f)
			{
				UI_Raycaster.RaycastScreenClamped(origin, Vector2.right * (Mathf.Sign(vector.x) * (num3 + num4 + 50f)), safeArea, 10f, out var hitPos);
				velocity_X.Position("\n\n" + Math.Abs((double)vector.x).ToVelocityString(), num4, hitPos, Vector2.right * Mathf.Sign(vector.x));
			}
			else
			{
				velocity_X.SetActive(active: false);
			}
			if (num5 > 0f)
			{
				UI_Raycaster.RaycastScreenClamped(origin, Vector2.up * (Mathf.Sign(vector.y) * (num3 + num5 + 50f)), safeArea, 10f, out var hitPos2);
				velocity_Y.Position("   " + Math.Abs((double)vector.y).ToVelocityString(), num5, hitPos2, Vector2.up * Mathf.Sign(vector.y));
			}
			else
			{
				velocity_Y.SetActive(active: false);
			}
		}
		else
		{*/
		float num6 = (float)location.velocity.magnitude;
		float num7 = GetArrowLength(num6) * 3f;
		if (num7 > 0f)
		{
			Double2 VectorSpeedTest;
			VectorSpeedTest.x = 0.0;
			VectorSpeedTest.y = 50.0;
			//Double2 @double = location.velocity.Rotate(0f - SFS.World.GameCamerasManager.main.world_Camera.CameraRotationRadians) / num6;

			Double2 @double = VectorSpeedTest.Rotate(0f - SFS.World.GameCamerasManager.main.world_Camera.CameraRotationRadians) / 50.0;

			UI_Raycaster.RaycastScreenClamped(origin, @double * 1000.0, safeArea, 10f, out var hitPos3);
			velocity_X.Position("", GetArrowLength(50.0f) * 3f, hitPos3, @double);


			//Vector2 hitPos3 = origin + @double.normalized*100;
			FileLog.Log("hitPos3 = " + hitPos3.x + "; " + hitPos3.y);

			velocity_X.Position("", GetArrowLength(50.0f) * 3f, hitPos3, @double);
		}
		else
		{
			velocity_X.SetActive(active: false);
		}
		velocity_Y.SetActive(active: false);
		/*}*/
	}

	private static float GetArrowLength(float velocity)
	{
		if (velocity < 0.1f)
		{
			return 0f;
		}
		if (velocity < 1f)
		{
			return velocity + 1f;
		}
		return Mathf.Log(velocity, 2f) + 2f;
	}
}

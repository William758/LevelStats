using System;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;

using System.Security;
using System.Security.Permissions;

[module: UnverifiableCode]
#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618 // Type or member is obsolete

namespace TPDespair.LevelStats
{
	[BepInPlugin(ModGuid, ModName, ModVer)]

	public class LevelStatsPlugin : BaseUnityPlugin
	{
		public const string ModVer = "1.1.1";
		public const string ModName = "LevelStats";
		public const string ModGuid = "com.TPDespair.LevelStats";

		public static ManualLogSource logSource;

		public static ConfigEntry<float> PlayerMovSpdLevel { get; set; }
		public static ConfigEntry<float> PlayerAtkSpdLevel { get; set; }
		public static ConfigEntry<float> PlayerDamageLevel { get; set; }
		public static ConfigEntry<float> PlayerHealthLevel { get; set; }
		public static ConfigEntry<float> PlayerRegenLevel { get; set; }
		public static ConfigEntry<float> PlayerArmorLevel { get; set; }



		public void Awake()
		{
			RoR2Application.isModded = true;
			NetworkModCompatibilityHelper.networkModList = NetworkModCompatibilityHelper.networkModList.Append(ModGuid + ":" + ModVer);

			logSource = Logger;
			SetupConfig(Config);

			StatHooks();
		}



		private static void SetupConfig(ConfigFile Config)
		{
			PlayerMovSpdLevel = Config.Bind(
				"PlayerStats", "PlayerMovSpdLevel", 0.015f,
				"Movement Speed increase granted per Level."
			);
			PlayerAtkSpdLevel = Config.Bind(
				"PlayerStats", "PlayerAtkSpdLevel", 0.01f,
				"Attack Speed increase granted per Level."
			);
			PlayerDamageLevel = Config.Bind(
				"PlayerStats", "PlayerDamageLevel", 0f,
				"Added Damage per Level. This is not a percent increase! Most survivors have around 12 (+2.4 per level) damage."
			);
			PlayerHealthLevel = Config.Bind(
				"PlayerStats", "PlayerHealthLevel", 0f,
				"Added Health per Level. This is not a percent increase! Most survivors have around 110 (+33 per level) health."
			);
			PlayerRegenLevel = Config.Bind(
				"PlayerStats", "PlayerRegenLevel", 0.1f,
				"Added Health Regeneration per Level. This is not a percent increase! Most survivors have around 1 hp/s (+0.2 hp/s per level) regeneration."
			);
			PlayerArmorLevel = Config.Bind(
				"PlayerStats", "PlayerArmorLevel", 0f,
				"Added Armor per Level. This is not a percent increase! Most survivors have 0 or 20 armor."
			);
		}



		private static void StatHooks()
		{
			MovementSpeedHook();
			AttackSpeedHook();
			DamageHook();
			HealthHook();
			RegenHook();
			ArmorHook();
		}

		private static bool IsPlayer(CharacterBody body)
		{
			return body.teamComponent.teamIndex == TeamIndex.Player;
		}

		private static void MovementSpeedHook()
		{
			IL.RoR2.CharacterBody.RecalculateStats += (il) =>
			{
				ILCursor c = new ILCursor(il);

				const int baseValue = 84;
				const int multValue = 85;
				const int divValue = 86;

				bool found = c.TryGotoNext(
					x => x.MatchLdloc(baseValue),
					x => x.MatchLdloc(multValue),
					x => x.MatchLdloc(divValue),
					x => x.MatchDiv(),
					x => x.MatchMul(),
					x => x.MatchStloc(baseValue)
				);

				if (found)
				{
					c.Index += 1;

					c.Emit(OpCodes.Pop);

					// increase
					c.Emit(OpCodes.Ldarg, 0);
					c.Emit(OpCodes.Ldloc, multValue);
					c.EmitDelegate<Func<CharacterBody, float, float>>((self, value) =>
					{
						if (IsPlayer(self))
						{
							value += PlayerMovSpdLevel.Value * (self.level - 1f);
						}

						return value;
					});
					c.Emit(OpCodes.Stloc, multValue);

					c.Emit(OpCodes.Ldloc, baseValue);
				}
				else
				{
					logSource.LogWarning("MovementSpeedHook Failed");
				}
			};
		}

		private static void AttackSpeedHook()
		{
			IL.RoR2.CharacterBody.RecalculateStats += (il) =>
			{
				ILCursor c = new ILCursor(il);

				const int baseValue = 96;
				const int multValue = 97;

				bool found = c.TryGotoNext(
					x => x.MatchLdloc(baseValue),
					x => x.MatchLdloc(multValue),
					x => x.MatchMul(),
					x => x.MatchStloc(baseValue)
				);

				if (found)
				{
					// increase
					c.Emit(OpCodes.Ldarg, 0);
					c.Emit(OpCodes.Ldloc, multValue);
					c.EmitDelegate<Func<CharacterBody, float, float>>((self, value) =>
					{
						if (IsPlayer(self))
						{
							value += PlayerAtkSpdLevel.Value * (self.level - 1f);
						}

						return value;
					});
					c.Emit(OpCodes.Stloc, multValue);
				}
				else
				{
					logSource.LogWarning("AttackSpeedHook Failed");
				}
			};
		}

		private static void DamageHook()
		{
			IL.RoR2.CharacterBody.RecalculateStats += (il) =>
			{
				ILCursor c = new ILCursor(il);

				const int baseValue = 88;
				const int multValue = 89;

				bool found = c.TryGotoNext(
					x => x.MatchLdloc(baseValue),
					x => x.MatchLdloc(multValue),
					x => x.MatchMul(),
					x => x.MatchStloc(baseValue)
				);

				if (found)
				{
					// add
					c.Emit(OpCodes.Ldarg, 0);
					c.Emit(OpCodes.Ldloc, baseValue);
					c.EmitDelegate<Func<CharacterBody, float, float>>((self, value) =>
					{
						if (IsPlayer(self))
						{
							value += PlayerDamageLevel.Value * (self.level - 1f);
						}

						return value;
					});
					c.Emit(OpCodes.Stloc, baseValue);
				}
				else
				{
					logSource.LogWarning("DamageHook Failed");
				}
			};
		}

		private static void HealthHook()
		{
			IL.RoR2.CharacterBody.RecalculateStats += (il) =>
			{
				ILCursor c = new ILCursor(il);

				const int baseValue = 70;
				const int multValue = 71;

				bool found = c.TryGotoNext(
					x => x.MatchLdloc(baseValue),
					x => x.MatchLdloc(multValue),
					x => x.MatchMul(),
					x => x.MatchStloc(baseValue)
				);

				if (found)
				{
					// add
					c.Emit(OpCodes.Ldarg, 0);
					c.Emit(OpCodes.Ldloc, baseValue);
					c.EmitDelegate<Func<CharacterBody, float, float>>((self, value) =>
					{
						if (IsPlayer(self))
						{
							value += PlayerHealthLevel.Value * (self.level - 1f);
						}

						return value;
					});
					c.Emit(OpCodes.Stloc, baseValue);
				}
				else
				{
					logSource.LogWarning("HealthHook Failed");
				}
			};
		}

		private static void RegenHook()
		{
			IL.RoR2.CharacterBody.RecalculateStats += (il) =>
			{
				ILCursor c = new ILCursor(il);

				const int lvlScaling = 75;
				const int knurlValue = 76;
				const int multValue = 82;

				bool found = c.TryGotoNext(
					x => x.MatchLdcR4(1f),
					x => x.MatchStloc(multValue)
				);

				if (found)
				{
					// add (affected by lvl regen scaling and ignites)
					c.Emit(OpCodes.Ldarg, 0);
					c.Emit(OpCodes.Ldloc, knurlValue);
					c.Emit(OpCodes.Ldloc, lvlScaling);
					c.EmitDelegate<Func<CharacterBody, float, float, float>>((self, value, scaling) =>
					{
						float amount = 0f;

						if (IsPlayer(self))
						{
							amount += PlayerRegenLevel.Value * (self.level - 1f);
						}

						if (amount != 0f)
						{
							value += amount * scaling;
						}

						return value;
					});
					c.Emit(OpCodes.Stloc, knurlValue);
				}
				else
				{
					logSource.LogWarning("RegenHook Failed");
				}
			};
		}

		private static void ArmorHook()
		{
			On.RoR2.CharacterBody.RecalculateStats += (orig, self) =>
			{
				orig(self);

				if (self && IsPlayer(self))
				{
					self.armor += PlayerArmorLevel.Value * (self.level - 1f);
				}
			};
		}
	}
}

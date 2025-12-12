using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using System.Linq;
using System.Security;
using System.Security.Permissions;
using UnityEngine;

[module: UnverifiableCode]
#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618 // Type or member is obsolete

namespace TPDespair.LevelStats
{
	[BepInPlugin(ModGuid, ModName, ModVer)]

	public class LevelStatsPlugin : BaseUnityPlugin
	{
		public const string ModVer = "1.2.0";
		public const string ModName = "LevelStats";
		public const string ModGuid = "com.TPDespair.LevelStats";

		public static ManualLogSource logSource;

		public static ConfigEntry<float> PlayerMovSpdLevel { get; set; }
		public static ConfigEntry<float> PlayerAtkSpdLevel { get; set; }
		public static ConfigEntry<float> PlayerDamageLevel { get; set; }
		public static ConfigEntry<float> PlayerHealthLevel { get; set; }
		public static ConfigEntry<float> PlayerRegenLevel { get; set; }
		public static ConfigEntry<float> PlayerArmorLevel { get; set; }



		public static int BaseRegenLocIndex = -1;



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

				int BaseSpeedLocIndex = -1;

				if (c.TryGotoNext(
					x => x.MatchLdfld<CharacterBody>("baseMoveSpeed"),
					x => x.MatchLdarg(0),
					x => x.MatchLdfld<CharacterBody>("levelMoveSpeed")
				))
				{
					if (!c.TryGotoNext(x => x.MatchStloc(out BaseSpeedLocIndex)))
					{
						logSource.LogWarning("MovementSpeedHook Failed - Could not find BaseSpeedLocIndex");
						return;
					}
				}



				int MultSpeedLocIndex = -1;
				int DivSpeedLocIndex = -1;

				bool found = c.TryGotoNext(
					x => x.MatchLdloc(BaseSpeedLocIndex),
					x => x.MatchLdloc(out MultSpeedLocIndex),
					x => x.MatchLdloc(out DivSpeedLocIndex),
					x => x.MatchDiv(),
					x => x.MatchMul(),
					x => x.MatchStloc(BaseSpeedLocIndex)
				);

				if (found)
				{
					c.Index += 1;

					c.Emit(OpCodes.Pop);

					// increase
					c.Emit(OpCodes.Ldarg, 0);
					c.Emit(OpCodes.Ldloc, MultSpeedLocIndex);
					c.EmitDelegate<Func<CharacterBody, float, float>>((self, value) =>
					{
						if (IsPlayer(self))
						{
							value += PlayerMovSpdLevel.Value * (self.level - 1f);
						}

						return value;
					});
					c.Emit(OpCodes.Stloc, MultSpeedLocIndex);

					c.Emit(OpCodes.Ldloc, BaseSpeedLocIndex);
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

				int BaseAtkSpdLocIndex = -1;

				if (c.TryGotoNext(
					x => x.MatchLdfld<CharacterBody>("baseAttackSpeed"),
					x => x.MatchLdarg(0),
					x => x.MatchLdfld<CharacterBody>("levelAttackSpeed")
				))
				{
					if (!c.TryGotoNext(x => x.MatchStloc(out BaseAtkSpdLocIndex)))
					{
						logSource.LogWarning("AttackSpeedHook Failed - Could not find BaseAtkSpdLocIndex");
						return;
					}
				}



				int MultAtkSpdLocIndex = -1;

				bool found = c.TryGotoNext(
					x => x.MatchLdloc(BaseAtkSpdLocIndex),
					x => x.MatchLdloc(out MultAtkSpdLocIndex),
					x => x.MatchMul(),
					x => x.MatchStloc(BaseAtkSpdLocIndex)
				);

				if (found)
				{
					// increase
					c.Emit(OpCodes.Ldarg, 0);
					c.Emit(OpCodes.Ldloc, MultAtkSpdLocIndex);
					c.EmitDelegate<Func<CharacterBody, float, float>>((self, value) =>
					{
						if (IsPlayer(self))
						{
							value += PlayerAtkSpdLevel.Value * (self.level - 1f);
						}

						return value;
					});
					c.Emit(OpCodes.Stloc, MultAtkSpdLocIndex);
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

				int BaseDamageLocIndex = -1;

				if (c.TryGotoNext(
					x => x.MatchLdfld<CharacterBody>("baseDamage"),
					x => x.MatchLdarg(0),
					x => x.MatchLdfld<CharacterBody>("levelDamage")
				))
				{
					if (!c.TryGotoNext(x => x.MatchStloc(out BaseDamageLocIndex)))
					{
						logSource.LogWarning("DamageHook Failed - Could not find BaseDamageLocIndex");
						return;
					}
				}



				int MultDamageLocIndex = -1;

				bool found = c.TryGotoNext(
					x => x.MatchLdloc(BaseDamageLocIndex),
					x => x.MatchLdloc(out MultDamageLocIndex),
					x => x.MatchMul(),
					x => x.MatchStloc(BaseDamageLocIndex)
				);

				if (found)
				{
					// add
					c.Emit(OpCodes.Ldarg, 0);
					c.Emit(OpCodes.Ldloc, BaseDamageLocIndex);
					c.EmitDelegate<Func<CharacterBody, float, float>>((self, value) =>
					{
						if (IsPlayer(self))
						{
							value += PlayerDamageLevel.Value * (self.level - 1f);
						}

						return value;
					});
					c.Emit(OpCodes.Stloc, BaseDamageLocIndex);
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

				int BaseHealthLocIndex = -1;

				if (c.TryGotoNext(
					x => x.MatchLdfld<CharacterBody>("baseMaxHealth"),
					x => x.MatchLdarg(0),
					x => x.MatchLdfld<CharacterBody>("levelMaxHealth")
				))
				{
					if (!c.TryGotoNext(x => x.MatchStloc(out BaseHealthLocIndex)))
					{
						logSource.LogWarning("HealthHook Failed - Could not find BaseHealthLocIndex");
						return;
					}
				}



				int MultHealthLocIndex = -1;

				bool found = c.TryGotoNext(
					x => x.MatchLdloc(BaseHealthLocIndex),
					x => x.MatchLdloc(out MultHealthLocIndex),
					x => x.MatchMul(),
					x => x.MatchStloc(BaseHealthLocIndex)
				);

				if (found)
				{
					// add
					c.Emit(OpCodes.Ldarg, 0);
					c.Emit(OpCodes.Ldloc, BaseHealthLocIndex);
					c.EmitDelegate<Func<CharacterBody, float, float>>((self, value) =>
					{
						if (IsPlayer(self))
						{
							value += PlayerHealthLevel.Value * (self.level - 1f);
						}

						return value;
					});
					c.Emit(OpCodes.Stloc, BaseHealthLocIndex);
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

				int findIndex = -1;

				if (c.TryGotoNext(
					x => x.MatchLdfld<CharacterBody>("baseRegen"),
					x => x.MatchLdarg(0),
					x => x.MatchLdfld<CharacterBody>("levelRegen")
				))
				{
					if (!c.TryGotoNext(x => x.MatchStloc(out BaseRegenLocIndex)))
					{
						logSource.LogWarning("RegenHook Failed - Could not find BaseRegenLocIndex");
						return;
					}
				}

				findIndex = c.Index;



				c.Index = 0;

				int KnurlCountLocIndex = -1;

				if (!c.TryGotoNext(
					x => x.MatchLdsfld(typeof(RoR2Content.Items).GetField("Knurl")),
					x => x.MatchCallOrCallvirt<Inventory>("GetItemCountEffective"),
					x => x.MatchStloc(out KnurlCountLocIndex)
				))
				{
					logSource.LogWarning("RegenHook Failed - Cound not find KnurlCountLocIndex");
					return;
				}



				c.Index = findIndex;

				int KnurlRegenLocIndex = -1;
				int LevelRegenScaleLocIndex = -1;

				bool found = c.TryGotoNext(
					x => x.MatchLdloc(KnurlCountLocIndex),
					x => x.MatchConvR4(),
					x => x.MatchLdcR4(out _),
					x => x.MatchMul(),
					x => x.MatchLdloc(out LevelRegenScaleLocIndex),
					x => x.MatchMul(),
					x => x.MatchStloc(out KnurlRegenLocIndex)
				);

				if (!found)
				{
					logSource.LogWarning("RegenHook - Could not find KnurlRegen region! , assuming indexes...");
					LevelRegenScaleLocIndex = BaseRegenLocIndex + 1;
					KnurlRegenLocIndex = -1;
				}
				else
				{
					logSource.LogWarning("RegenHook - KnurlRegenLocIndex: " + KnurlRegenLocIndex + ", LevelRegenScaleLocIndex: " + LevelRegenScaleLocIndex);

					if (KnurlRegenLocIndex - LevelRegenScaleLocIndex > 1)
					{
						logSource.LogWarning("RegenHook - These indexes probably shouldnt be this far apart?");
					}

				}



				int CrocoRegenLocIndex = -1;

				found = c.TryGotoNext(
					x => x.MatchLdsfld(typeof(RoR2Content.Buffs).GetField("CrocoRegen")),
					x => x.MatchCallOrCallvirt<CharacterBody>("GetBuffCount")
				) && c.TryGotoNext(
					x => x.MatchStloc(out CrocoRegenLocIndex));

				if (!found)
				{
					logSource.LogWarning("RegenHook Failed - Could not find CrocoRegen Region!");
					return;
				}



				if (KnurlRegenLocIndex == -1)
				{
					KnurlRegenLocIndex = CrocoRegenLocIndex + 1;
				}



				found = c.TryGotoNext(
					x => x.MatchLdcR4(1f),
					x => x.MatchStloc(out _)
				);

				if (found)
				{
					// add (affected by lvl regen scaling and ignites)
					c.Emit(OpCodes.Ldarg, 0);
					c.Emit(OpCodes.Ldloc, KnurlRegenLocIndex);
					c.Emit(OpCodes.Ldloc, LevelRegenScaleLocIndex);
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
					c.Emit(OpCodes.Stloc, KnurlRegenLocIndex);
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

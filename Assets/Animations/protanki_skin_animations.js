(function () {
  if (window.__ptSkinAnimatorLoaded) return;
  window.__ptSkinAnimatorLoaded = true;

  const STORAGE_KEY = "pt_skin_anim_cfg_v2";
  const TOGGLE_KEY = "__TS_SKIN_ANIM_HOTKEY__";

  const ANIM_SKINS = [
    // --- ISIDA DC ---
    {
      group: "isida_dc",
      label: "Isida DC",
      name: "Isida DC (indexed)",
      vertexCount: 3398,
      parts: {
        cover_t: { start: 2724, end: 3094, pivot: [0.0, 86.0, 44.5] },
        cover_r: { start: 3094, end: 3236, pivot: [22.0, 82.0, 25.0] },
        cover_l: { start: 3236, end: 3378, pivot: [-22.0, 82.0, 25.0] },
        pump: { start: 3378, end: 3388, pivot: [0.0, -17.18, 83.97] },
        plunger: { start: 3388, end: 3398, pivot: [0.0, -58.1, 66.5] },
      },
    },
    {
      group: "isida_dc",
      label: "Isida DC",
      name: "Isida DC (non-indexed)",
      vertexCount: 12465,
      parts: {
        cover_t: { start: 9999, end: 11373, pivot: [0.0, 86.0, 44.5] },
        cover_r: { start: 11373, end: 11895, pivot: [22.0, 82.0, 25.0] },
        cover_l: { start: 11895, end: 12417, pivot: [-22.0, 82.0, 25.0] },
        pump: { start: 12417, end: 12441, pivot: [0.0, -17.18, 83.97] },
        plunger: { start: 12441, end: 12465, pivot: [0.0, -58.1, 66.5] },
      },
    },
    // --- HAMMER SP ---
    {
      group: "hammer",
      label: "Hammer SP",
      name: "Hammer SP (indexed)",
      vertexCount: 4050,
      parts: {
        barrel: { start: 2384, end: 2798, pivot: [0.0, 149.37, 66.38] },
        turret_bac: { start: 2798, end: 2955, pivot: [0.0, -107.58, 58.73] },
        cover: { start: 2955, end: 3322, pivot: [0.0, -60.0, 61.33] },
        casing_1: { start: 3322, end: 3372, pivot: [-24.14, -23.49, 64.98] },
        casing_2: { start: 3372, end: 3434, pivot: [24.14, -23.49, 64.98] },
        cover_2: { start: 3434, end: 3948, pivot: [0.0, -65.0, 103.83] },
        gear: { start: 3948, end: 4050, pivot: [0.0, -35.2832, 47.063] },
      },
    },
    {
      group: "hammer",
      label: "Hammer SP",
      name: "Hammer SP (non-indexed)",
      vertexCount: 12645,
      parts: {
        barrel: { start: 7464, end: 8835, pivot: [0.0, 149.37, 66.38] },
        turret_bac: { start: 8835, end: 9324, pivot: [0.0, -107.58, 58.73] },
        cover: { start: 9324, end: 10317, pivot: [0.0, -60.0, 61.33] },
        casing_1: { start: 10317, end: 10449, pivot: [-24.14, -23.49, 64.98] },
        casing_2: { start: 10449, end: 10617, pivot: [24.14, -23.49, 64.98] },
        cover_2: { start: 10617, end: 12369, pivot: [0.0, -65.0, 103.83] },
        gear: { start: 12369, end: 12645, pivot: [0.0, -35.2832, 47.063] },
      },
    },
    // --- HAMMER IC ---
    {
      group: "hammer",
      label: "Hammer IC",
      name: "Hammer IC (indexed)",
      vertexCount: 3824,
      parts: {
        barrel: { start: 2052, end: 2598, pivot: [0.0, 149.37, 66.38] },
        turret_bac: { start: 2598, end: 2772, pivot: [0.0, -107.58, 58.73] },
        cover: { start: 2772, end: 3207, pivot: [0.0, -58.0, 60.96] },
        casing_1: { start: 3207, end: 3271, pivot: [-23.5, -30.88, 66.41] },
        casing_2: { start: 3271, end: 3335, pivot: [23.5, -30.88, 66.41] },
        cover_2: { start: 3335, end: 3824, pivot: [0.0, -68.0, 103.83] },
      },
    },
    {
      group: "hammer",
      label: "Hammer IC",
      name: "Hammer IC (non-indexed)",
      vertexCount: 13713,
      parts: {
        barrel: { start: 7479, end: 9735, pivot: [0.0, 149.37, 66.38] },
        turret_bac: { start: 9735, end: 10263, pivot: [0.0, -107.58, 58.73] },
        cover: { start: 10263, end: 11727, pivot: [0.0, -58.0, 60.96] },
        casing_1: { start: 11727, end: 11907, pivot: [-23.5, -30.88, 66.41] },
        casing_2: { start: 11907, end: 12087, pivot: [23.5, -30.88, 66.41] },
        cover_2: { start: 12087, end: 13713, pivot: [0.0, -68.0, 103.83] },
      },
    },
    // --- SMOKY VT ---
    {
      group: "smoky_vt",
      label: "Smoky VT",
      name: "Smoky VT (legacy indexed)",
      vertexCount: 2675,
      parts: {
        barrel: { start: 2180, end: 2358, pivot: [0.0, 157.9663, 36.9077] },
        turret_bac: { start: 2358, end: 2532, pivot: [0.0, -97.9794, 35.3442] },
        cover: { start: 2532, end: 2675, pivot: [42.5284, -37.2361, 72.0069] },
      },
    },
    {
      group: "smoky_vt",
      label: "Smoky VT",
      name: "Smoky VT (legacy non-indexed)",
      vertexCount: 8898,
      parts: {
        barrel: { start: 7404, end: 7890, pivot: [0.0, 157.9663, 36.9077] },
        turret_bac: { start: 7890, end: 8418, pivot: [0.0, -97.9794, 35.3442] },
        cover: { start: 8418, end: 8898, pivot: [42.5284, -37.2361, 72.0069] },
      },
    },
    {
      group: "smoky_vt",
      label: "Smoky VT",
      name: "Smoky VT (indexed)",
      vertexCount: 2738,
      parts: {
        barrel: { start: 2180, end: 2358, pivot: [0.0, 157.9663, 36.9077] },
        turret_bac: { start: 2358, end: 2532, pivot: [0.0, -97.9794, 35.3442] },
        cover: { start: 2532, end: 2675, pivot: [42.5284, -37.2361, 72.0069] },
        shell: { start: 2675, end: 2738, pivot: [50.1866, -16.5344, 53.0132] },
      },
    },
    {
      group: "smoky_vt",
      label: "Smoky VT",
      name: "Smoky VT (non-indexed)",
      vertexCount: 9102,
      parts: {
        barrel: { start: 7404, end: 7890, pivot: [0.0, 157.9663, 36.9077] },
        turret_bac: { start: 7890, end: 8418, pivot: [0.0, -97.9794, 35.3442] },
        cover: { start: 8418, end: 8898, pivot: [42.5284, -37.2361, 72.0069] },
        shell: { start: 8898, end: 9102, pivot: [50.1866, -16.5344, 53.0132] },
      },
    },
    // --- THUNDER XT HD ---
    {
      group: "thunder_xt_hd",
      label: "Thunder XT HD",
      name: "Thunder XT HD (indexed)",
      vertexCount: 4268,
      parts: {
        shell: { start: 2912, end: 3166, pivot: [0.0, 31.021, 65.839] },
        barrel: { start: 3166, end: 4010, pivot: [0.0, 172.976, 52.837] },
        cover_l: { start: 4010, end: 4139, pivot: [-31.226, 25.506, 77.771] },
        cover_r: { start: 4139, end: 4268, pivot: [31.226, 25.506, 77.771] },
      },
    },
    {
      group: "thunder_xt_hd",
      label: "Thunder XT HD",
      name: "Thunder XT HD (non-indexed)",
      vertexCount: 10545,
      parts: {
        shell: { start: 7443, end: 8199, pivot: [0.0, 31.021, 65.839] },
        barrel: { start: 8199, end: 10029, pivot: [0.0, 172.976, 52.837] },
        cover_l: { start: 10029, end: 10287, pivot: [-31.226, 25.506, 77.771] },
        cover_r: { start: 10287, end: 10545, pivot: [31.226, 25.506, 77.771] },
      },
    },
    // --- TWINS TX SP ---
    {
      group: "twins_tx_sp",
      label: "Twins TX SP",
      name: "Twins TX SP (indexed)",
      vertexCount: 4938,
      parts: {
        reel_r: { start: 3074, end: 3191, pivot: [63.696, -53.201, 47.77] },
        barrel_r: { start: 3191, end: 4006, pivot: [62.827, 108.391, 35.981] },
        barrel_l: { start: 4006, end: 4821, pivot: [-64.521, 108.436, 35.981] },
        reel_l: { start: 4821, end: 4938, pivot: [-63.576, -53.201, 47.77] },
      },
    },
    {
      group: "twins_tx_sp",
      label: "Twins TX SP",
      name: "Twins TX SP (non-indexed)",
      vertexCount: 15042,
      parts: {
        reel_r: { start: 9594, end: 9918, pivot: [63.696, -53.201, 47.77] },
        barrel_r: { start: 9918, end: 12318, pivot: [62.827, 108.391, 35.981] },
        barrel_l: { start: 12318, end: 14718, pivot: [-64.521, 108.436, 35.981] },
        reel_l: { start: 14718, end: 15042, pivot: [-63.576, -53.201, 47.77] },
      },
    },
    // --- TWINS TX XT ---
    {
      group: "twins_tx_xt",
      label: "Twins TX XT",
      name: "Twins TX XT (indexed)",
      vertexCount: 5692,
      parts: {
        barrel_l: { start: 2388, end: 3411, pivot: [-62.047, 62.896, 30.985] },
        reel_l: { start: 3411, end: 4040, pivot: [-62.188, -41.264, 51.496] },
        barrel_r: { start: 4040, end: 5063, pivot: [64.302, 62.896, 30.985] },
        reel_r: { start: 5063, end: 5692, pivot: [62.131, -41.264, 51.496] },
      },
    },
    {
      group: "twins_tx_xt",
      label: "Twins TX XT",
      name: "Twins TX XT (non-indexed)",
      vertexCount: 12234,
      parts: {
        barrel_l: { start: 5130, end: 7296, pivot: [-62.047, 62.896, 30.985] },
        reel_l: { start: 7296, end: 8682, pivot: [-62.188, -41.264, 51.496] },
        barrel_r: { start: 8682, end: 10848, pivot: [64.302, 62.896, 30.985] },
        reel_r: { start: 10848, end: 12234, pivot: [62.131, -41.264, 51.496] },
      },
    },
    // --- HUNTER DC HULL ---
    {
      group: "hunter_dc",
      label: "Hunter DC",
      name: "Hunter DC (indexed)",
      vertexCount: 7186,
      trackStart: 6694,
      trackEnd: 7186,
      parts: {
        wheel_lf: { start: 5354, end: 5409, pivot: [-860.177, -4066.111, 66.523], side: -1 },
        wheel_l1: { start: 5409, end: 5521, pivot: [-860.903, -4137.741, 41.506], side: -1 },
        wheel_l2: { start: 5521, end: 5633, pivot: [-860.903, -4225.393, 41.506], side: -1 },
        wheel_l3: { start: 5633, end: 5745, pivot: [-860.903, -4311.712, 41.506], side: -1 },
        wheel_l4: { start: 5745, end: 5857, pivot: [-860.903, -4399.004, 41.506], side: -1 },
        wheel_l5: { start: 5857, end: 5969, pivot: [-860.903, -4485.958, 41.506], side: -1 },
        // wheel_lb / wheel_rb are non-circular (30.19 x 33.11) and clip through the back of the tracks when rotated.
        wheel_rf: { start: 6024, end: 6079, pivot: [-612.952, -4066.111, 66.523], side: 1 },
        wheel_r1: { start: 6079, end: 6191, pivot: [-612.225, -4137.741, 41.506], side: 1 },
        wheel_r2: { start: 6191, end: 6303, pivot: [-612.225, -4225.393, 41.506], side: 1 },
        wheel_r3: { start: 6303, end: 6415, pivot: [-612.225, -4311.712, 41.506], side: 1 },
        wheel_r4: { start: 6415, end: 6527, pivot: [-612.225, -4399.004, 41.506], side: 1 },
        wheel_r5: { start: 6527, end: 6639, pivot: [-612.225, -4485.958, 41.506], side: 1 },
      },
    },
    {
      group: "hunter_dc",
      label: "Hunter DC",
      name: "Hunter DC (non-indexed)",
      vertexCount: 17064,
      trackStart: 16344,
      trackEnd: 17064,
      parts: {
        wheel_lf: { start: 13488, end: 13572, pivot: [-860.177, -4066.111, 66.523], side: -1 },
        wheel_l1: { start: 13572, end: 13824, pivot: [-860.903, -4137.741, 41.506], side: -1 },
        wheel_l2: { start: 13824, end: 14076, pivot: [-860.903, -4225.393, 41.506], side: -1 },
        wheel_l3: { start: 14076, end: 14328, pivot: [-860.903, -4311.712, 41.506], side: -1 },
        wheel_l4: { start: 14328, end: 14580, pivot: [-860.903, -4399.004, 41.506], side: -1 },
        wheel_l5: { start: 14580, end: 14832, pivot: [-860.903, -4485.958, 41.506], side: -1 },
        wheel_rf: { start: 14916, end: 15000, pivot: [-612.952, -4066.111, 66.523], side: 1 },
        wheel_r1: { start: 15000, end: 15252, pivot: [-612.225, -4137.741, 41.506], side: 1 },
        wheel_r2: { start: 15252, end: 15504, pivot: [-612.225, -4225.393, 41.506], side: 1 },
        wheel_r3: { start: 15504, end: 15756, pivot: [-612.225, -4311.712, 41.506], side: 1 },
        wheel_r4: { start: 15756, end: 16008, pivot: [-612.225, -4399.004, 41.506], side: 1 },
        wheel_r5: { start: 16008, end: 16260, pivot: [-612.225, -4485.958, 41.506], side: 1 },
      },
    },
  ];

  const TWINS_PRESETS = {
    alternating_recoil: {
      label: "Alternating Recoil",
      desc: "Left and right barrels fire in sequence with matching reel spin",
      eval(open, phase, intensity, side) {
        const t = Math.min(1.0, Math.max(0.0, phase / (Math.PI * 2.0)));
        const kick = Math.pow(1.0 - t, 2.0) * open * intensity;
        const spin = (0.7 + 5.0 * t) * open * intensity;
        return {
          barrelLY: side === 0 ? -24.0 * kick : 0,
          barrelRY: side === 1 ? -24.0 * kick : 0,
          reelL: side === 0 ? spin : 0,
          reelR: side === 1 ? spin : 0,
        };
      },
    },
    dual_burst: {
      label: "Dual Burst",
      desc: "Both barrels recoil together with synchronized reel movement",
      eval(open, phase, intensity) {
        const t = Math.min(1.0, Math.max(0.0, phase / (Math.PI * 2.0)));
        const kick = Math.pow(1.0 - t, 1.9) * open * intensity;
        const spin = (0.6 + 4.0 * t) * open * intensity;
        return {
          barrelLY: -18.0 * kick,
          barrelRY: -18.0 * kick,
          reelL: spin,
          reelR: spin,
        };
      },
    },
    subtle: {
      label: "Subtle Recoil",
      desc: "Small alternating barrel kick with restrained reel movement",
      eval(open, phase, intensity, side) {
        const t = Math.min(1.0, Math.max(0.0, phase / (Math.PI * 2.0)));
        const kick = Math.pow(1.0 - t, 2.0) * open * intensity;
        const spin = 2.5 * t * open * intensity;
        return {
          barrelLY: side === 0 ? -9.0 * kick : 0,
          barrelRY: side === 1 ? -9.0 * kick : 0,
          reelL: side === 0 ? spin : 0,
          reelR: side === 1 ? spin : 0,
        };
      },
    },
  };

  function smokyCycle(phase, intensity, strength, speed = 1.0) {
    const t = Math.min(1.0, Math.max(0.0, phase / (Math.PI * 2.0) * speed));
    const kick = Math.sin(Math.PI * Math.min(1.0, t / 0.28)) * Math.pow(1.0 - t, 2.0);
    const hatch = Math.min(1.0, t / 0.16) * Math.min(1.0, Math.max(0.0, (0.78 - t) / 0.18));
    const flight = Math.min(1.0, Math.max(0.0, (t - 0.20) / 0.58));
    return {
      barrelY: -26.0 * kick * intensity * strength,
      rearY: -8.0 * kick * intensity * strength,
      rearPitch: 0.05 * kick * intensity * strength,
      coverOpen: 1.10 * hatch * Math.min(1.25, Math.max(0.85, intensity * strength)),
      shellVisible: t >= 0.20 && t < 0.82 && intensity > 0,
      // Keep every travel component moving away from the hatch; hide only after clearance.
      shellX: 8.0 + 125.0 * flight * intensity * strength,
      shellY: -8.0 - 22.0 * flight * intensity * strength,
      shellZ: 8.0 + 42.0 * flight * intensity * strength,
      shellSpin: 10.0 * flight,
    };
  }

  const GROUP_META = {
    isida_dc: {
      title: "Isida DC",
      defaultPreset: "classic",
      presets: {
        classic: {
          label: "Classic Jaw Open",
          desc: "Balanced top/side claw opening + rear piston cycle",
          eval(open, phase, intensity) {
            const pulse = Math.sin(phase);
            return {
              top: (0.24 + 0.03 * pulse) * open * intensity,
              right: (0.20 + 0.025 * pulse) * open * intensity,
              left: (0.20 + 0.025 * pulse) * open * intensity,
              pump: 4.5 * open * Math.sin(phase * 1.4) * intensity,
              plunger: -5.5 * open * Math.sin(phase * 1.4) * intensity,
            };
          },
        },
        predator: {
          label: "Predator Snap",
          desc: "Wide aggressive jaw flare with high-energy vibration",
          eval(open, phase, intensity) {
            const vib = Math.sin(phase * 2.8) * 0.035;
            return {
              top: (0.34 + vib) * open * intensity,
              right: (0.30 + vib * 0.8) * open * intensity,
              left: (0.30 + vib * 0.8) * open * intensity,
              pump: 6.2 * open * Math.sin(phase * 2.1) * intensity,
              plunger: -7.0 * open * Math.sin(phase * 2.1) * intensity,
            };
          },
        },
        overdrive: {
          label: "Overdrive Surge",
          desc: "Deep rhythmic breathing waves synced with rear pistons",
          eval(open, phase, intensity) {
            const wave = 0.5 + 0.5 * Math.sin(phase * 0.85);
            return {
              top: (0.14 + 0.20 * wave) * open * intensity,
              right: (0.12 + 0.17 * wave) * open * intensity,
              left: (0.12 + 0.17 * wave) * open * intensity,
              pump: 6.0 * open * Math.sin(phase * 0.85) * intensity,
              plunger: -6.8 * open * Math.sin(phase * 0.85) * intensity,
            };
          },
        },
        flutter: {
          label: "Twin-Wing Flutter",
          desc: "Alternating side mandible motion while top jaw holds open",
          eval(open, phase, intensity) {
            const rWave = Math.sin(phase * 1.3);
            const lWave = Math.sin(phase * 1.3 + 1.4);
            return {
              top: (0.22 + 0.025 * Math.sin(phase * 2.6)) * open * intensity,
              right: (0.19 + 0.085 * rWave) * open * intensity,
              left: (0.19 + 0.085 * lWave) * open * intensity,
              pump: 4.8 * open * Math.sin(phase * 1.9) * intensity,
              plunger: -5.4 * open * Math.sin(phase * 1.9) * intensity,
            };
          },
        },
        recoil: {
          label: "Plasma Recoil",
          desc: "Sharp forward kick pulses with slow mechanical return",
          eval(open, phase, intensity) {
            const t = ((phase * 0.45) % (Math.PI * 2)) / (Math.PI * 2);
            const kick = Math.pow(1.0 - t, 2.2);
            return {
              top: (0.15 + 0.18 * kick) * open * intensity,
              right: (0.12 + 0.16 * kick) * open * intensity,
              left: (0.12 + 0.16 * kick) * open * intensity,
              pump: (5.5 * kick - 2.0) * open * intensity,
              plunger: (-6.5 * kick + 2.2) * open * intensity,
            };
          },
        },
        subtle: {
          label: "Subtle Mechanical",
          desc: "Clean, restrained opening for minimal distraction",
          eval(open, phase, intensity) {
            const pulse = Math.sin(phase * 0.9);
            return {
              top: (0.14 + 0.015 * pulse) * open * intensity,
              right: (0.12 + 0.012 * pulse) * open * intensity,
              left: (0.12 + 0.012 * pulse) * open * intensity,
              pump: 2.8 * open * pulse * intensity,
              plunger: -3.2 * open * pulse * intensity,
            };
          },
        },
      },
    },
    hammer: {
      title: "Hammer SP / IC",
      oneShot: true,
      defaultPreset: "heavy_recoil",
      presets: {
        heavy_recoil: {
          label: "Heavy Recoil",
          desc: "Sharp shotgun barrel kickback, breach vents open, gear ratchets",
          eval(open, phase, intensity) {
            const t = ((phase * 0.35) % (Math.PI * 2)) / (Math.PI * 2);
            const kick = Math.pow(1.0 - t, 2.0);
            return {
              barrelY: -(12.0 + 14.0 * kick) * open * intensity,
              rearY: -(6.0 + 8.0 * kick) * open * intensity,
              coverAngle: (0.10 + 0.12 * kick) * open * intensity,
              cover2Angle: (0.12 + 0.14 * kick) * open * intensity,
              casingOut: (4.5 + 6.5 * kick) * open * intensity,
              casingUp: (2.0 + 3.0 * kick) * open * intensity,
              gearAngle: phase * 0.9 * open * intensity,
            };
          },
        },
        burst_cycle: {
          label: "Rapid Burst",
          desc: "Fast cyclic barrel pump with alternating left/right shell kicks",
          eval(open, phase, intensity) {
            const cyc = 0.5 + 0.5 * Math.sin(phase * 1.4);
            const alt = Math.sin(phase * 0.7);
            return {
              barrelY: -(6.0 + 18.0 * cyc) * open * intensity,
              rearY: -(4.0 + 9.0 * cyc) * open * intensity,
              coverAngle: (0.08 + 0.14 * cyc) * open * intensity,
              cover2Angle: (0.10 + 0.16 * cyc) * open * intensity,
              casingOut: (4.0 + 5.0 * Math.abs(alt)) * open * intensity,
              casingUp: (2.5 + 2.5 * cyc) * open * intensity,
              gearAngle: phase * 1.4 * open * intensity,
            };
          },
        },
        breach_open: {
          label: "Breach & Vent",
          desc: "Wide top & side breach opening with deep barrel retraction",
          eval(open, phase, intensity) {
            const wave = 0.5 + 0.5 * Math.sin(phase * 0.8);
            return {
              barrelY: -(16.0 + 8.0 * wave) * open * intensity,
              rearY: -(10.0 + 4.0 * wave) * open * intensity,
              coverAngle: (0.20 + 0.08 * wave) * open * intensity,
              cover2Angle: (0.24 + 0.10 * wave) * open * intensity,
              casingOut: (8.0 + 4.0 * wave) * open * intensity,
              casingUp: (4.0 + 2.0 * wave) * open * intensity,
              gearAngle: phase * 0.65 * open * intensity,
            };
          },
        },
        subtle: {
          label: "Subtle Recoil",
          desc: "Clean, restrained barrel kick and gear turn",
          eval(open, phase, intensity) {
            const cyc = 0.5 + 0.5 * Math.sin(phase);
            return {
              barrelY: -(6.0 + 6.0 * cyc) * open * intensity,
              rearY: -(3.0 + 3.0 * cyc) * open * intensity,
              coverAngle: 0.06 * open * intensity,
              cover2Angle: 0.08 * open * intensity,
              casingOut: 2.5 * open * intensity,
              casingUp: 1.0 * open * intensity,
              gearAngle: phase * 0.5 * open * intensity,
            };
          },
        },
      },
    },
    smoky_vt: {
      title: "Smoky VT",
      oneShot: true,
      cycleDuration: 0.80,
      defaultPreset: "shell_eject",
      presets: {
        shell_eject: {
          label: "Shell Ejection",
          desc: "Recoil, open the hatch, and throw a spinning shell out of the side",
          eval(open, phase, intensity) {
            return smokyCycle(phase, intensity, 1.0);
          },
        },
        heavy_cannon: {
          label: "Heavy Cannon",
          desc: "Heavy recoil and a stronger shell throw through the open hatch",
          eval(open, phase, intensity) {
            return smokyCycle(phase, intensity, 1.25);
          },
        },
        rapid_cycle: {
          label: "Rapid Cycle",
          desc: "Compact recoil and an early shell ejection",
          eval(open, phase, intensity) {
            return smokyCycle(phase, intensity, 0.85, 1.3);
          },
        },
        subtle: {
          label: "Subtle Recoil",
          desc: "Restrained recoil with a short shell throw",
          eval(open, phase, intensity) {
            return smokyCycle(phase, intensity, 0.55);
          },
        },
      },
    },
    thunder_xt_hd: {
      title: "Thunder XT HD",
      oneShot: true,
      cycleDuration: 0.85,
      defaultPreset: "heavy_recoil",
      presets: {
        heavy_recoil: {
          label: "Heavy Recoil",
          desc: "Deep barrel kick, paired side covers open, and shell ejects",
          eval(open, phase, intensity) {
            const t = Math.min(1.0, Math.max(0.0, phase / (Math.PI * 2.0)));
            const kick = Math.pow(1.0 - t, 2.1);
            const arc = Math.sin(Math.PI * Math.min(1.0, t * 1.1));
            return {
              barrelY: -(10.0 + 24.0 * kick) * open * intensity,
              coverOpen: (0.10 + 0.24 * arc) * open * intensity,
              shellX: 24.0 * arc * open * intensity,
              shellY: -8.0 * t * open * intensity,
              shellZ: 16.0 * arc * open * intensity,
              shellSpin: 4.2 * t * open * intensity,
            };
          },
        },
        rapid_cycle: {
          label: "Rapid Cycle",
          desc: "Fast Thunder recoil with a compact breech and shell cycle",
          eval(open, phase, intensity) {
            const t = Math.min(1.0, Math.max(0.0, phase / (Math.PI * 2.0)));
            const kick = Math.pow(1.0 - t, 1.8);
            const arc = Math.sin(Math.PI * t);
            return {
              barrelY: -(6.0 + 18.0 * kick) * open * intensity,
              coverOpen: (0.08 + 0.18 * arc) * open * intensity,
              shellX: 16.0 * arc * open * intensity,
              shellY: -5.0 * t * open * intensity,
              shellZ: 10.0 * arc * open * intensity,
              shellSpin: 6.0 * t * open * intensity,
            };
          },
        },
        subtle: {
          label: "Subtle Recoil",
          desc: "Restrained barrel kick and slight cover movement",
          eval(open, phase, intensity) {
            const cyc = Math.sin(phase * 0.5);
            return {
              barrelY: -(4.0 + 9.0 * cyc) * open * intensity,
              coverOpen: 0.10 * cyc * open * intensity,
              shellX: 5.0 * cyc * open * intensity,
              shellY: -1.5 * cyc * open * intensity,
              shellZ: 3.0 * cyc * open * intensity,
              shellSpin: 0.8 * cyc * open * intensity,
            };
          },
        },
      },
    },
    twins_tx_sp: {
      title: "Twins TX SP",
      oneShot: true,
      cycleDuration: 0.32,
      repeatFire: true,
      defaultPreset: "alternating_recoil",
      presets: TWINS_PRESETS,
    },
    twins_tx_xt: {
      title: "Twins TX XT",
      oneShot: true,
      cycleDuration: 0.32,
      repeatFire: true,
      defaultPreset: "alternating_recoil",
      presets: TWINS_PRESETS,
    },
    hunter_dc: {
      title: "Hunter DC",
      isHull: true,
      defaultPreset: "forward",
      presets: {
        forward: {
          label: "Demonic Drive",
          desc: "Beating heart, breathing demon skulls, snapping fangs, wheels & treads",
          eval() { return { left: 1, right: 1 }; },
        },
        reverse: {
          label: "Reverse",
          desc: "Reverse treads & wheels with living demonic armor",
          eval() { return { left: -1, right: -1 }; },
        },
        pivot: {
          label: "Pivot Turn",
          desc: "Counter-rotate tracks with living demonic armor",
          eval() { return { left: -1, right: 1 }; },
        },
      },
    },
  };

  function loadConfig() {
    const defaults = {
      activeGroup: "isida_dc",
      presets: {
        isida_dc: "classic",
        hammer: "heavy_recoil",
        smoky_vt: "shell_eject",
        thunder_xt_hd: "heavy_recoil",
        twins_tx_sp: "alternating_recoil",
        twins_tx_xt: "alternating_recoil",
        hunter_dc: "forward",
      },
      trigger: "shoot",
      hullTrigger: "always",
      speed: 1.0,
      intensity: 1.0,
      menuHidden: true,
    };
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return defaults;
      const parsed = JSON.parse(raw);
      const presets = { ...defaults.presets };
      if (parsed.presets && typeof parsed.presets === "object") {
        for (const g of Object.keys(GROUP_META)) {
          if (GROUP_META[g].presets[parsed.presets[g]]) {
            presets[g] = parsed.presets[g];
          }
        }
      }
      return {
        activeGroup: GROUP_META[parsed.activeGroup] ? parsed.activeGroup : defaults.activeGroup,
        presets,
        trigger: parsed.trigger === "always" ? "always" : "shoot",
        hullTrigger: parsed.hullTrigger === "shoot" || parsed.hullTrigger === "off" ? parsed.hullTrigger : "always",
        speed: typeof parsed.speed === "number" ? Math.min(2.5, Math.max(0.4, parsed.speed)) : defaults.speed,
        intensity: typeof parsed.intensity === "number" ? Math.min(1.8, Math.max(0.4, parsed.intensity)) : defaults.intensity,
        menuHidden: typeof parsed.menuHidden === "boolean" ? parsed.menuHidden : defaults.menuHidden,
      };
    } catch {
      return defaults;
    }
  }

  const cfg = loadConfig();
  function saveConfig() {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(cfg));
    } catch {}
  }

  const tracked = [];
  const hunterUploads = new WeakMap();
  let isFiring = false;
  let openAmount = 0.0;
  let phase = 0.0;
  let hammerShotProg = 1.0; // 1.0 = idle/finished, 0.0..1.0 = playing one-shot cycle
  let hammerGearAccum = 0.0;
  const shotStates = {};
  for (const [group, meta] of Object.entries(GROUP_META)) {
    if (meta.cycleDuration) shotStates[group] = { progress: 1.0, side: 1 };
  }
  let lastTime = performance.now();

  let isEmbedded = !!window.__ptSkinAnimEmbedded;
  let initialized = false;

  function isTypingInTextInput() {
    const el = document.activeElement;
    if (!el) return false;
    if (el.id === "__postfx_ui" && el.shadowRoot && el.shadowRoot.activeElement) {
      const sa = el.shadowRoot.activeElement;
      if (sa.tagName === "INPUT" || sa.tagName === "TEXTAREA") return true;
    }
    if (el.closest && el.closest("#pt-anim-panel")) return false;
    const tag = el.tagName;
    if (tag === "INPUT") {
      const t = (el.type || "text").toLowerCase();
      return t !== "range" && t !== "checkbox" && t !== "radio" && t !== "button";
    }
    return tag === "TEXTAREA" || !!el.isContentEditable;
  }

  function triggerShot() {
    hammerShotProg = 0.0;
    for (const state of Object.values(shotStates)) {
      state.progress = 0.0;
      state.side = 1 - state.side;
    }
  }

  window.addEventListener("keydown", (e) => {
    if (e.code === "Space") {
      if (isTypingInTextInput()) return;
      if (!e.repeat) triggerShot();
      isFiring = true;
    }
    if (!isEmbedded && e.code === TOGGLE_KEY) {
      e.preventDefault();
      cfg.menuHidden = !cfg.menuHidden;
      saveConfig();
      syncMenuVisibility();
    }
  }, true);

  window.addEventListener("keyup", (e) => {
    if (e.code === "Space") isFiring = false;
  }, true);

  window.addEventListener("mousedown", (e) => {
    if (e.button === 0 && document.pointerLockElement && !isTypingInTextInput()) {
      triggerShot();
      isFiring = true;
    }
  }, true);

  window.addEventListener("mouseup", (e) => {
    if (e.button === 0) isFiring = false;
  }, true);

  window.addEventListener("blur", () => {
    isFiring = false;
  }, true);

  function matchSkin(arr) {
    for (const skin of ANIM_SKINS) {
      if (arr.length !== skin.vertexCount * 3) continue;
      if (skin.group === "hunter_dc") {
        let minX = Infinity;
        let maxX = -Infinity;
        let minY = Infinity;
        let maxY = -Infinity;
        let minZ = Infinity;
        let maxZ = -Infinity;
        for (let i = 0; i < arr.length; i += 3) {
          minX = Math.min(minX, arr[i]);
          maxX = Math.max(maxX, arr[i]);
          minY = Math.min(minY, arr[i + 1]);
          maxY = Math.max(maxY, arr[i + 1]);
          minZ = Math.min(minZ, arr[i + 2]);
          maxZ = Math.max(maxZ, arr[i + 2]);
        }
        // The client uploads coordinates as (x, original z, -original y).
        // Hunter's source dimensions are x=330, y=514.35, z=148.5.
        const width = maxX - minX;
        if (!(width > 0)) continue;
        const scale = width / 330.0;
        if (Math.abs((maxY - minY) / scale - 148.5) > 1.0 ||
            Math.abs((maxZ - minZ) / scale - 514.35) > 2.0) continue;
        return { skin, scale };
      }
      let maxAbs = 0;
      for (let i = 0; i < arr.length; i += 3) {
        const ax = Math.abs(arr[i]);
        const ay = Math.abs(arr[i + 1]);
        const az = Math.abs(arr[i + 2]);
        if (ax > maxAbs) maxAbs = ax;
        if (ay > maxAbs) maxAbs = ay;
        if (az > maxAbs) maxAbs = az;
      }
      if (maxAbs < 1.5 || maxAbs > 500) continue;
      const scale = maxAbs < 10.0 ? 0.01 : 1.0;
      return { skin, scale };
    }
    return null;
  }

  function prepareHunter(skin, rest) {
    let minX = Infinity, maxX = -Infinity;
    let minY = Infinity;
    let minZ = Infinity, maxZ = -Infinity;
    for (let i = 0; i < rest.length; i += 3) {
      if (rest[i] < minX) minX = rest[i];
      if (rest[i] > maxX) maxX = rest[i];
      if (rest[i + 1] < minY) minY = rest[i + 1];
      if (rest[i + 2] < minZ) minZ = rest[i + 2];
      if (rest[i + 2] > maxZ) maxZ = rest[i + 2];
    }
    const wheels = Object.entries(skin.parts).map(([name, part]) => {
      const min = [Infinity, Infinity, Infinity], max = [-Infinity, -Infinity, -Infinity];
      for (let i = part.start * 3; i < part.end * 3; i += 3) {
        for (let axis = 0; axis < 3; axis++) {
          min[axis] = Math.min(min[axis], rest[i + axis]);
          max[axis] = Math.max(max[axis], rest[i + axis]);
        }
      }
      return { ...part, pivot: min.map((v, axis) => (v + max[axis]) * 0.5),
        radius: (max[1] - min[1]) * 0.5 };
    });
    const hullEnd = wheels.length ? wheels[0].start : skin.trackStart;
    return {
      wheels,
      hullEnd,
      centerX: (minX + maxX) * 0.5,
      minY,
      centerZ: (minZ + maxZ) * 0.5,
      distance: 0,
      time: 0,
      uv: null,
    };
  }

  function connectHunterUvs(gl) {
    const candidates = tracked.filter(item => item.gl === gl && item.hunter && !item.hunter.uv);
    if (!candidates.length) return;
    const program = gl.getParameter(gl.CURRENT_PROGRAM);
    if (!program) return;
    let item = null;
    const uvCandidates = [];
    const activeCount = gl.getProgramParameter(program, gl.ACTIVE_ATTRIBUTES);
    for (let i = 0; i < activeCount; i++) {
      const attr = gl.getActiveAttrib(program, i);
      if (!attr) continue;
      const index = gl.getAttribLocation(program, attr.name);
      if (index < 0 ||
          !gl.getVertexAttrib(index, gl.VERTEX_ATTRIB_ARRAY_ENABLED) ||
          gl.getVertexAttrib(index, gl.VERTEX_ATTRIB_ARRAY_TYPE) !== gl.FLOAT ||
          gl.getVertexAttribOffset(index, gl.VERTEX_ATTRIB_ARRAY_POINTER) !== 0) continue;
      const size = gl.getVertexAttrib(index, gl.VERTEX_ATTRIB_ARRAY_SIZE);
      const stride = gl.getVertexAttrib(index, gl.VERTEX_ATTRIB_ARRAY_STRIDE);
      if (stride !== 0 && stride !== size * 4) continue;
      const buffer = gl.getVertexAttrib(index, gl.VERTEX_ATTRIB_ARRAY_BUFFER_BINDING);
      if (!buffer) continue;
      if (size === 3 && !item) {
        item = candidates.find(candidate => candidate.buffer === buffer) || null;
      } else if (size === 2 && hunterUploads.has(buffer)) {
        uvCandidates.push(buffer);
      }
    }
    if (!item || !uvCandidates.length) return;
    for (const uv of uvCandidates) {
      const rest = hunterUploads.get(uv);
      if (!rest || rest.length !== item.skin.vertexCount * 2) continue;
      // Tread U repeats horizontally. V must stay in its own half of the atlas.
      let minV = Infinity, maxV = -Infinity, minU = Infinity, maxU = -Infinity;
      for (let i = item.skin.trackStart; i < item.skin.trackEnd; i++) {
        const u = rest[i * 2], v = rest[i * 2 + 1];
        minU = Math.min(minU, u); maxU = Math.max(maxU, u);
        minV = Math.min(minV, v); maxV = Math.max(maxV, v);
      }
      if (maxU - minU > 2 && minV >= -0.05 && maxV <= 1.05 && maxV - minV < 0.5) {
        item.hunter.uv = { buffer: uv, rest, work: new Float32Array(rest), dirty: false };
        updateStatusBadge();
        return;
      }
    }
  }

  function bell2(v, lo, hi) {
    if (v <= lo || v >= hi) return 0;
    const s = Math.sin(Math.PI * ((v - lo) / (hi - lo)));
    return s * s;
  }

  function smooth01(v, lo, hi) {
    if (v <= lo) return 0;
    if (v >= hi) return 1;
    const t = (v - lo) / (hi - lo);
    return t * t * (3 - 2 * t);
  }

  function updateHunter(item, pose, dt) {
    const { gl, skin, scale, rest, work, hunter } = item;
    hunter.distance += dt * 85.0 * cfg.speed * cfg.intensity;
    hunter.time += dt * cfg.speed;

    // 1. Living demonic hull deformations (smooth C1 spatial masks, zero seam tearing)
    const inten = cfg.intensity;
    const phase = (hunter.time * 1.15) % 1.0;
    const lub = Math.exp(-Math.pow((phase - 0.14) / 0.065, 2));
    const dub = 0.65 * Math.exp(-Math.pow((phase - 0.31) / 0.075, 2));
    const beat = (lub + dub) * inten;
    const breath = (0.5 + 0.5 * Math.sin(hunter.time * 2.6)) * inten;
    const snarl = Math.sin(hunter.time * 3.8) * inten;

    const { hullEnd, centerX, minY, centerZ } = hunter;
    const invScale = 1.0 / scale;
    for (let i = 0; i < hullEnd; i++) {
      const idx = i * 3;
      const rx = (rest[idx] - centerX) * invScale;
      const h = (rest[idx + 1] - minY) * invScale;
      const fwd = (centerZ - rest[idx + 2]) * invScale;

      // Front hood glowing ribcage: rhythmic lub-dub heartbeat
      if (fwd > 55 && fwd < 195 && h > 95 && rx > -78 && rx < 78) {
        const w = bell2(rx, -78, 78) * bell2(fwd, 55, 195) * smooth01(h, 95, 125);
        if (w > 0) {
          work[idx] += (rx / 78.0) * beat * 2.8 * w * scale;
          work[idx + 1] += beat * 4.8 * w * scale;
        }
      }

      // Front bumper fangs & mandibles: snarl / flare outward
      if (fwd > 195 && h > 45 && h < 88 && rx > -92 && rx < 92) {
        const w = bell2(rx, -92, 92) * smooth01(fwd, 195, 235) * bell2(h, 45, 88);
        if (w > 0) {
          const sx = rx >= 0 ? 1 : -1;
          work[idx] += sx * snarl * 4.2 * w * scale;
          work[idx + 1] += snarl * 2.2 * w * scale;
          work[idx + 2] -= snarl * 3.5 * w * scale;
        }
      }

      // Rear demon skulls & fleshy exhaust tubes: breathing heave & flare
      if (fwd < -125 && h > 95 && rx > -72 && rx < 72) {
        const w = bell2(rx, -72, 72) * smooth01(-fwd, 125, 195) * smooth01(h, 95, 125);
        if (w > 0) {
          const sx = rx >= 0 ? 1 : -1;
          work[idx] += sx * breath * 2.6 * w * scale;
          work[idx + 1] += breath * 4.5 * w * scale;
          work[idx + 2] += breath * 2.8 * w * scale;
        }
      }
    }

    // 2. Road wheels & front sprockets
    for (const wheel of hunter.wheels) {
      const direction = wheel.side < 0 ? pose.left : pose.right;
      // Both sides rotate around the same world X axis.
      rotateX(rest, work, wheel.start, wheel.end, wheel.pivot,
        hunter.distance * scale * direction / wheel.radius);
    }

    // 3. Scrolling track tread UVs
    const uv = hunter.uv;
    if (!uv) return;
    if (!gl.isBuffer(uv.buffer)) { hunter.uv = null; return; }
    uv.work.set(uv.rest);
    const middle = (skin.trackStart + skin.trackEnd) / 2;
    for (let i = skin.trackStart; i < skin.trackEnd; i++) {
      const direction = i < middle ? pose.left : pose.right;
      uv.work[i * 2] = uv.rest[i * 2] - (hunter.distance * direction / 36.0) % 1.0;
    }
    const previous = gl.getParameter(gl.ARRAY_BUFFER_BINDING);
    gl.bindBuffer(gl.ARRAY_BUFFER, uv.buffer);
    gl.bufferSubData(gl.ARRAY_BUFFER, 0, uv.work);
    gl.bindBuffer(gl.ARRAY_BUFFER, previous);
    uv.dirty = true;
  }

  function restoreHunter(item) {
    const hunter = item.hunter;
    if (!hunter) return;
    hunter.distance = 0;
    hunter.time = 0;
    const uv = hunter.uv;
    if (!uv || !uv.dirty || !item.gl.isBuffer(uv.buffer)) return;
    uv.work.set(uv.rest);
    const gl = item.gl, previous = gl.getParameter(gl.ARRAY_BUFFER_BINDING);
    gl.bindBuffer(gl.ARRAY_BUFFER, uv.buffer);
    gl.bufferSubData(gl.ARRAY_BUFFER, 0, uv.rest);
    gl.bindBuffer(gl.ARRAY_BUFFER, previous);
    uv.dirty = false;
  }

  function hookGL(Proto) {
    if (!Proto || !Proto.bufferData) return;
    const origBufferData = Proto.bufferData;
    Proto.bufferData = function (target, srcData, usage, ...restArgs) {
      if (target === this.ARRAY_BUFFER && srcData instanceof Float32Array) {
        if (srcData.length === 14372 || srcData.length === 34128) {
          const buffer = this.getParameter(this.ARRAY_BUFFER_BINDING);
          if (buffer) hunterUploads.set(buffer, new Float32Array(srcData));
        }
        const hit = matchSkin(srcData);
        if (hit) {
          const buf = this.getParameter(this.ARRAY_BUFFER_BINDING);
          if (buf) {
            const idle = new Float32Array(srcData);
            if (hit.skin.group === "smoky_vt") hideSmokyShell(hit.skin, hit.scale, idle);
            tracked.push({
              gl: this,
              buffer: buf,
              skin: hit.skin,
              scale: hit.scale,
              rest: new Float32Array(srcData),
              work: new Float32Array(srcData),
              idle,
              hunter: hit.skin.group === "hunter_dc" ? prepareHunter(hit.skin, srcData) : null,
              dirty: false,
            });
            cfg.activeGroup = hit.skin.group;
            saveConfig();
            refreshMenuState();
            srcData = idle;
            usage = this.DYNAMIC_DRAW;
          }
        }
      }
      return origBufferData.call(this, target, srcData, usage, ...restArgs);
    };
    for (const name of ["drawElements", "drawArrays"]) {
      const original = Proto[name];
      if (!original) continue;
      Proto[name] = function (...args) {
        connectHunterUvs(this);
        return original.apply(this, args);
      };
    }
  }

  hookGL(window.WebGL2RenderingContext && WebGL2RenderingContext.prototype);
  hookGL(window.WebGLRenderingContext && WebGLRenderingContext.prototype);

  function rotateX(rest, work, start, end, pivot, angle) {
    const c = Math.cos(angle);
    const s = Math.sin(angle);
    const py = pivot[1];
    const pz = pivot[2];
    for (let i = start * 3; i < end * 3; i += 3) {
      const dy = rest[i + 1] - py;
      const dz = rest[i + 2] - pz;
      work[i] = rest[i];
      work[i + 1] = py + dy * c - dz * s;
      work[i + 2] = pz + dy * s + dz * c;
    }
  }

  function rotateZ(rest, work, start, end, pivot, angle) {
    const c = Math.cos(angle);
    const s = Math.sin(angle);
    const px = pivot[0];
    const py = pivot[1];
    for (let i = start * 3; i < end * 3; i += 3) {
      const dx = rest[i] - px;
      const dy = rest[i + 1] - py;
      work[i] = px + dx * c - dy * s;
      work[i + 1] = py + dx * s + dy * c;
      work[i + 2] = rest[i + 2];
    }
  }

  function rotateY(rest, work, start, end, pivot, angle) {
    const c = Math.cos(angle);
    const s = Math.sin(angle);
    const px = pivot[0];
    const pz = pivot[2];
    for (let i = start * 3; i < end * 3; i += 3) {
      const dx = rest[i] - px;
      const dz = rest[i + 2] - pz;
      work[i] = px + dx * c + dz * s;
      work[i + 1] = rest[i + 1];
      work[i + 2] = pz - dx * s + dz * c;
    }
  }

  function translateWork(work, start, end, dx, dy, dz) {
    for (let i = start * 3; i < end * 3; i += 3) {
      work[i] += dx;
      work[i + 1] += dy;
      work[i + 2] += dz;
    }
  }

  function translate(rest, work, start, end, dx, dy, dz) {
    for (let i = start * 3; i < end * 3; i += 3) {
      work[i] = rest[i] + dx;
      work[i + 1] = rest[i + 1] + dy;
      work[i + 2] = rest[i + 2] + dz;
    }
  }

  function applyIsidaPose(skin, scale, rest, work, pose) {
    const p = skin.parts;
    const sp = (piv) => [piv[0] * scale, piv[1] * scale, piv[2] * scale];
    const pumpShift = pose.pump * scale;
    const plungerShift = pose.plunger * scale;

    rotateX(rest, work, p.cover_t.start, p.cover_t.end, sp(p.cover_t.pivot), pose.top);
    rotateZ(rest, work, p.cover_r.start, p.cover_r.end, sp(p.cover_r.pivot), -pose.right);
    rotateZ(rest, work, p.cover_l.start, p.cover_l.end, sp(p.cover_l.pivot), pose.left);
    translate(rest, work, p.pump.start, p.pump.end, 0, pumpShift * 0.92, pumpShift * 0.39);
    translate(rest, work, p.plunger.start, p.plunger.end, 0, plungerShift * 0.92, plungerShift * 0.39);
  }

  function applyHammerPose(skin, scale, rest, work, pose) {
    const p = skin.parts;
    const sp = (piv) => [piv[0] * scale, piv[1] * scale, piv[2] * scale];

    translate(rest, work, p.barrel.start, p.barrel.end, 0, pose.barrelY * scale, 0);
    translate(rest, work, p.turret_bac.start, p.turret_bac.end, 0, pose.rearY * scale, 0);
    rotateX(rest, work, p.cover.start, p.cover.end, sp(p.cover.pivot), pose.coverAngle);
    rotateX(rest, work, p.cover_2.start, p.cover_2.end, sp(p.cover_2.pivot), pose.cover2Angle);
    translate(rest, work, p.casing_1.start, p.casing_1.end, -pose.casingOut * scale, 0, pose.casingUp * scale);
    translate(rest, work, p.casing_2.start, p.casing_2.end, pose.casingOut * scale, 0, pose.casingUp * scale);
    if (p.gear) {
      rotateX(rest, work, p.gear.start, p.gear.end, sp(p.gear.pivot), pose.gearAngle);
    }
  }

  function hideSmokyShell(skin, scale, work) {
    const shell = skin.parts.shell;
    if (!shell) return;
    // Degenerate triangles are invisible; keep the point inside the turret.
    for (let i = shell.start * 3; i < shell.end * 3; i += 3) {
      work[i] = 0;
      work[i + 1] = 0;
      work[i + 2] = 30.0 * scale;
    }
  }

  function applySmokyPose(skin, scale, rest, work, pose) {
    const p = skin.parts;
    const sp = (piv) => [piv[0] * scale, piv[1] * scale, piv[2] * scale];

    translate(rest, work, p.barrel.start, p.barrel.end, 0, pose.barrelY * scale, 0);
    rotateX(rest, work, p.turret_bac.start, p.turret_bac.end, sp(p.turret_bac.pivot), pose.rearPitch);
    translateWork(work, p.turret_bac.start, p.turret_bac.end, 0, pose.rearY * scale, 0);
    rotateY(rest, work, p.cover.start, p.cover.end, sp(p.cover.pivot), -pose.coverOpen);
    if (p.shell && pose.shellVisible) {
      rotateY(rest, work, p.shell.start, p.shell.end, sp(p.shell.pivot), pose.shellSpin);
      translateWork(work, p.shell.start, p.shell.end, pose.shellX * scale, pose.shellY * scale, pose.shellZ * scale);
    } else {
      hideSmokyShell(skin, scale, work);
    }
  }

  function applyThunderPose(skin, scale, rest, work, pose) {
    const p = skin.parts;
    const sp = (piv) => [piv[0] * scale, piv[1] * scale, piv[2] * scale];

    translate(rest, work, p.barrel.start, p.barrel.end, 0, pose.barrelY * scale, 0);
    rotateY(rest, work, p.cover_l.start, p.cover_l.end, sp(p.cover_l.pivot), -pose.coverOpen);
    rotateY(rest, work, p.cover_r.start, p.cover_r.end, sp(p.cover_r.pivot), pose.coverOpen);
    rotateY(rest, work, p.shell.start, p.shell.end, sp(p.shell.pivot), pose.shellSpin);
    translateWork(work, p.shell.start, p.shell.end, pose.shellX * scale, pose.shellY * scale, pose.shellZ * scale);
  }

  function applyTwinsPose(skin, scale, rest, work, pose) {
    const p = skin.parts;
    const sp = (piv) => [piv[0] * scale, piv[1] * scale, piv[2] * scale];

    translate(rest, work, p.barrel_l.start, p.barrel_l.end, 0, pose.barrelLY * scale, 0);
    translate(rest, work, p.barrel_r.start, p.barrel_r.end, 0, pose.barrelRY * scale, 0);
    rotateY(rest, work, p.reel_l.start, p.reel_l.end, sp(p.reel_l.pivot), pose.reelL);
    rotateY(rest, work, p.reel_r.start, p.reel_r.end, sp(p.reel_r.pivot), -pose.reelR);
  }

  function tick(now) {
    const dt = Math.min(0.1, (now - lastTime) * 0.001);
    lastTime = now;

    const active = cfg.trigger === "always" || isFiring;
    const targetOpen = active ? 1.0 : 0.0;
    const rate = active ? 9.0 : 7.0;
    openAmount += (targetOpen - openAmount) * Math.min(1.0, dt * rate);

    const contIdle = !active && openAmount < 0.0005;
    if (contIdle) {
      openAmount = 0.0;
      phase = 0.0;
    } else {
      phase += dt * 10.0 * cfg.speed;
    }

    if (hammerShotProg < 1.0) {
      hammerShotProg = Math.min(1.0, hammerShotProg + (dt * cfg.speed) / 0.60);
    }

    for (const [group, state] of Object.entries(shotStates)) {
      const meta = GROUP_META[group];
      const repeat = cfg.trigger === "always" || (meta.repeatFire && isFiring);
      if (state.progress >= 1.0 && repeat) {
        state.progress = 0.0;
        state.side = 1 - state.side;
      }
      if (state.progress < 1.0) {
        state.progress = Math.min(1.0, state.progress + dt * cfg.speed / meta.cycleDuration);
      }
    }

    const poseCache = {};
    let removedAny = false;

    for (let idx = tracked.length - 1; idx >= 0; idx--) {
      const item = tracked[idx];
      const { gl, buffer, skin, scale, rest, work } = item;
      if (gl.isContextLost() || !gl.isBuffer(buffer)) {
        restoreHunter(item);
        tracked.splice(idx, 1);
        removedAny = true;
        continue;
      }

      const grp = skin.group;
      const gmeta = GROUP_META[grp];
      const useOneShot = !!(gmeta && gmeta.oneShot) && cfg.trigger === "shoot";
      const state = shotStates[grp];
      const progress = state ? state.progress : hammerShotProg;
      const grpIdle = gmeta.isHull
        ? (cfg.hullTrigger === "off" || (cfg.hullTrigger === "shoot" && !isFiring))
        : (state ? progress >= 1.0 : (useOneShot ? progress >= 1.0 : contIdle));

      if (grpIdle) {
        restoreHunter(item);
        if (!item.dirty) continue;
        const prev = gl.getParameter(gl.ARRAY_BUFFER_BINDING);
        gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
        gl.bufferSubData(gl.ARRAY_BUFFER, 0, item.idle);
        gl.bindBuffer(gl.ARRAY_BUFFER, prev);
        item.dirty = false;
        continue;
      }

      if (!poseCache[grp]) {
        const gmeta = GROUP_META[grp];
        const pkey = cfg.presets[grp] || gmeta.defaultPreset;
        const preset = gmeta.presets[pkey] || gmeta.presets[gmeta.defaultPreset];
        if (state) {
          const shotOpen = Math.sin(Math.PI * Math.pow(progress, 0.42));
          poseCache[grp] = preset.eval(shotOpen, progress * Math.PI * 2.0, cfg.intensity, state.side);
        } else if (useOneShot) {
          const shotOpen = Math.sin(Math.PI * Math.pow(hammerShotProg, 0.42));
          const shotPhase = hammerShotProg * Math.PI * 2.0;
          poseCache[grp] = preset.eval(shotOpen, shotPhase, cfg.intensity);
        } else {
          poseCache[grp] = preset.eval(openAmount, phase, cfg.intensity);
        }
      }

      work.set(rest);
      if (grp === "isida_dc") {
        applyIsidaPose(skin, scale, rest, work, poseCache[grp]);
      } else if (grp === "hammer") {
        applyHammerPose(skin, scale, rest, work, poseCache[grp]);
      } else if (grp === "smoky_vt") {
        applySmokyPose(skin, scale, rest, work, poseCache[grp]);
      } else if (grp === "thunder_xt_hd") {
        applyThunderPose(skin, scale, rest, work, poseCache[grp]);
      } else if (grp === "twins_tx_sp" || grp === "twins_tx_xt") {
        applyTwinsPose(skin, scale, rest, work, poseCache[grp]);
      } else if (grp === "hunter_dc") {
        updateHunter(item, poseCache[grp], dt);
      }

      const prev = gl.getParameter(gl.ARRAY_BUFFER_BINDING);
      gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
      gl.bufferSubData(gl.ARRAY_BUFFER, 0, work);
      gl.bindBuffer(gl.ARRAY_BUFFER, prev);
      item.dirty = true;
    }

    if (removedAny) refreshMenuState();
    requestAnimationFrame(tick);
  }

  // --- IN-GAME MENU ---
  let menuRoot = null;
  let statusBadge = null;
  let groupTabsEl = null;
  let presetsGridEl = null;
  let descEl = null;

  function selectedStatus(group) {
    const g = GROUP_META[group] ? group : cfg.activeGroup;
    const hit = tracked.find((t) => t.skin.group === g);
    const title = GROUP_META[g].title;
    if (!hit) return { text: `${title}: Not detected`, ready: false };
    if (hit.hunter) {
      if (cfg.hullTrigger === "off") return { text: "Hunter DC: Off", ready: false };
      if (cfg.hullTrigger === "shoot" && !isFiring) return { text: "Hunter DC: On Shoot", ready: true };
      if (!hit.hunter.uv) return { text: "Hunter DC: Wheels ready", ready: true };
    }
    return { text: `${hit.skin.label}: Ready`, ready: true };
  }

  function updateStatusBadge() {
    if (!statusBadge) return;
    const status = selectedStatus();
    statusBadge.textContent = status.text;
    statusBadge.title = cfg.activeGroup === "hunter_dc" ?
      "Select Always (Preview) to animate. Wheels ready means tread UVs are still awaiting association." : "";
    statusBadge.style.color = status.ready ? "#4ade80" : "#fbbf24";
    statusBadge.style.borderColor = status.ready ? "rgba(74,222,128,0.35)" : "rgba(251,191,36,0.35)";
    statusBadge.style.background = status.ready ? "rgba(74,222,128,0.12)" : "rgba(251,191,36,0.10)";
  }

  function renderGroupAndPresets() {
    if (!groupTabsEl || !presetsGridEl || !descEl) return;
    groupTabsEl.innerHTML = "";
    for (const [gkey, gmeta] of Object.entries(GROUP_META)) {
      const tab = document.createElement("button");
      tab.className = "pt-anim-btn" + (cfg.activeGroup === gkey ? " active" : "");
      tab.style.textAlign = "center";
      tab.textContent = gmeta.title;
      tab.onclick = () => {
        cfg.activeGroup = gkey;
        saveConfig();
        refreshMenuState();
      };
      groupTabsEl.appendChild(tab);
    }

    const gmeta = GROUP_META[cfg.activeGroup] || GROUP_META.isida_dc;
    const activePreset = cfg.presets[cfg.activeGroup] || gmeta.defaultPreset;
    presetsGridEl.innerHTML = "";
    for (const [pkey, p] of Object.entries(gmeta.presets)) {
      const btn = document.createElement("button");
      btn.className = "pt-anim-btn" + (activePreset === pkey ? " active" : "");
      btn.textContent = p.label;
      btn.onclick = () => {
        cfg.presets[cfg.activeGroup] = pkey;
        saveConfig();
        renderGroupAndPresets();
      };
      presetsGridEl.appendChild(btn);
    }

    descEl.textContent = (gmeta.presets[activePreset] || gmeta.presets[gmeta.defaultPreset]).desc;
    if (gmeta.isHull) {
      descEl.textContent += cfg.hullTrigger === "always" ?
        ". Hull animations run independently of turret trigger." :
        (cfg.hullTrigger === "shoot" ? ". Animating on shoot." : ". Hull animations turned off.");
    }
  }

  function refreshMenuState() {
    updateStatusBadge();
    renderGroupAndPresets();
  }

  function syncMenuVisibility() {
    if (!menuRoot) return;
    menuRoot.style.display = cfg.menuHidden ? "none" : "flex";
  }

  function buildMenu() {
    if (menuRoot || !document.body) return;

    const style = document.createElement("style");
    style.textContent = `
      #pt-anim-panel {
        position: fixed;
        top: 64px;
        right: 16px;
        width: 288px;
        max-height: calc(100vh - 96px);
        overflow-y: auto;
        z-index: 999998;
        display: flex;
        flex-direction: column;
        gap: 8px;
        padding: 12px;
        border-radius: 10px;
        background: rgba(14, 18, 26, 0.90);
        border: 1px solid rgba(255, 255, 255, 0.14);
        box-shadow: 0 12px 32px rgba(0, 0, 0, 0.55);
        backdrop-filter: blur(10px);
        color: #e5e7eb;
        font-family: "Segoe UI", system-ui, sans-serif;
        font-size: 12px;
        user-select: none;
      }
      #pt-anim-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        cursor: move;
        padding-bottom: 6px;
        border-bottom: 1px solid rgba(255,255,255,0.1);
      }
      .pt-anim-title {
        font-weight: 700;
        font-size: 13px;
        color: #f9fafb;
        letter-spacing: 0.2px;
      }
      .pt-anim-badge {
        font-size: 10px;
        font-weight: 600;
        padding: 2px 7px;
        border-radius: 999px;
        border: 1px solid transparent;
      }
      .pt-anim-presets {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 6px;
      }
      .pt-anim-btn {
        padding: 7px 8px;
        border-radius: 7px;
        border: 1px solid rgba(255,255,255,0.12);
        background: rgba(255,255,255,0.05);
        color: #d1d5db;
        font-size: 11px;
        font-weight: 600;
        text-align: left;
        cursor: pointer;
        transition: all 0.14s ease;
      }
      .pt-anim-btn:hover {
        background: rgba(255,255,255,0.12);
        color: #fff;
      }
      .pt-anim-btn.active {
        background: rgba(59, 130, 246, 0.28);
        border-color: rgba(96, 165, 250, 0.75);
        color: #ffffff;
      }
      .pt-anim-row {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 8px;
      }
      .pt-anim-seg {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 5px;
      }
      .pt-anim-slider {
        width: 100%;
        accent-color: #60a5fa;
        cursor: pointer;
      }
      .pt-anim-desc {
        font-size: 11px;
        color: #9ca3af;
        line-height: 1.35;
        min-height: 28px;
      }
      .pt-anim-close {
        background: none;
        border: none;
        color: #9ca3af;
        cursor: pointer;
        font-size: 13px;
        padding: 0 4px;
      }
      .pt-anim-close:hover { color: #fff; }
    `;
    document.head.appendChild(style);

    menuRoot = document.createElement("div");
    menuRoot.id = "pt-anim-panel";

    const header = document.createElement("div");
    header.id = "pt-anim-header";
    const title = document.createElement("span");
    title.className = "pt-anim-title";
    title.textContent = `Skin Animations [${TOGGLE_KEY}]`;

    const rightBox = document.createElement("div");
    rightBox.style.display = "flex";
    rightBox.style.alignItems = "center";
    rightBox.style.gap = "6px";

    statusBadge = document.createElement("span");
    statusBadge.className = "pt-anim-badge";

    const closeBtn = document.createElement("button");
    closeBtn.className = "pt-anim-close";
    closeBtn.textContent = "✕";
    closeBtn.title = `Hide (${TOGGLE_KEY})`;
    closeBtn.onclick = () => {
      cfg.menuHidden = true;
      saveConfig();
      syncMenuVisibility();
    };

    rightBox.appendChild(statusBadge);
    rightBox.appendChild(closeBtn);
    header.appendChild(title);
    header.appendChild(rightBox);
    menuRoot.appendChild(header);

    groupTabsEl = document.createElement("div");
    groupTabsEl.className = "pt-anim-seg";
    groupTabsEl.style.gridTemplateColumns = "repeat(2, 1fr)";
    menuRoot.appendChild(groupTabsEl);

    presetsGridEl = document.createElement("div");
    presetsGridEl.className = "pt-anim-presets";
    menuRoot.appendChild(presetsGridEl);

    descEl = document.createElement("div");
    descEl.className = "pt-anim-desc";
    menuRoot.appendChild(descEl);

    const trigLabel = document.createElement("div");
    trigLabel.style.fontSize = "11px";
    trigLabel.style.color = "#9ca3af";
    trigLabel.textContent = "Trigger Mode";
    menuRoot.appendChild(trigLabel);

    const trigSeg = document.createElement("div");
    trigSeg.className = "pt-anim-seg";
    const btnShoot = document.createElement("button");
    btnShoot.className = "pt-anim-btn" + (cfg.trigger === "shoot" ? " active" : "");
    btnShoot.textContent = "On Shoot";
    btnShoot.style.textAlign = "center";

    const btnAlways = document.createElement("button");
    btnAlways.className = "pt-anim-btn" + (cfg.trigger === "always" ? " active" : "");
    btnAlways.textContent = "Always (Preview)";
    btnAlways.style.textAlign = "center";

    function setTrigger(mode) {
      cfg.trigger = mode;
      saveConfig();
      btnShoot.classList.toggle("active", mode === "shoot");
      btnAlways.classList.toggle("active", mode === "always");
      refreshMenuState();
    }
    btnShoot.onclick = () => setTrigger("shoot");
    btnAlways.onclick = () => setTrigger("always");
    trigSeg.appendChild(btnShoot);
    trigSeg.appendChild(btnAlways);
    menuRoot.appendChild(trigSeg);

    function addSlider(labelText, min, max, step, getVal, setVal) {
      const wrap = document.createElement("div");
      const row = document.createElement("div");
      row.className = "pt-anim-row";
      const lbl = document.createElement("span");
      lbl.textContent = labelText;
      lbl.style.color = "#9ca3af";
      const valLbl = document.createElement("span");
      valLbl.textContent = getVal().toFixed(2) + "x";
      valLbl.style.color = "#f3f4f6";
      valLbl.style.fontWeight = "600";
      row.appendChild(lbl);
      row.appendChild(valLbl);

      const input = document.createElement("input");
      input.type = "range";
      input.className = "pt-anim-slider";
      input.min = String(min);
      input.max = String(max);
      input.step = String(step);
      input.value = String(getVal());
      input.oninput = () => {
        const v = parseFloat(input.value);
        setVal(v);
        valLbl.textContent = v.toFixed(2) + "x";
        saveConfig();
      };
      wrap.appendChild(row);
      wrap.appendChild(input);
      menuRoot.appendChild(wrap);
    }

    addSlider("Speed", 0.4, 2.5, 0.05, () => cfg.speed, (v) => { cfg.speed = v; });
    addSlider("Intensity", 0.4, 1.8, 0.05, () => cfg.intensity, (v) => { cfg.intensity = v; });

    let dragging = false;
    let dragOX = 0;
    let dragOY = 0;
    header.addEventListener("mousedown", (e) => {
      if (e.target === closeBtn) return;
      dragging = true;
      const rect = menuRoot.getBoundingClientRect();
      dragOX = e.clientX - rect.left;
      dragOY = e.clientY - rect.top;
      menuRoot.style.right = "auto";
      menuRoot.style.left = rect.left + "px";
      menuRoot.style.top = rect.top + "px";
    });
    window.addEventListener("mousemove", (e) => {
      if (!dragging) return;
      menuRoot.style.left = Math.max(0, Math.min(window.innerWidth - 290, e.clientX - dragOX)) + "px";
      menuRoot.style.top = Math.max(0, Math.min(window.innerHeight - 60, e.clientY - dragOY)) + "px";
    });
    window.addEventListener("mouseup", () => { dragging = false; });

    document.body.appendChild(menuRoot);
    refreshMenuState();
    syncMenuVisibility();
  }

  function init(opts) {
    if (opts && typeof opts.embedded === "boolean") {
      isEmbedded = opts.embedded;
    }
    if (!isEmbedded) {
      if (document.body) {
        buildMenu();
      } else {
        window.addEventListener("DOMContentLoaded", buildMenu, { once: true });
      }
    }
    if (!initialized) {
      initialized = true;
      requestAnimationFrame(tick);
    }
  }

  function getConfig() {
    return {
      activeGroup: cfg.activeGroup,
      presets: { ...cfg.presets },
      trigger: cfg.trigger,
      hullTrigger: cfg.hullTrigger,
      speed: cfg.speed,
      intensity: cfg.intensity,
    };
  }

  function setConfig(partial) {
    if (!partial || typeof partial !== "object") return;
    if (partial.activeGroup && GROUP_META[partial.activeGroup]) {
      cfg.activeGroup = partial.activeGroup;
    }
    if (partial.trigger === "always" || partial.trigger === "shoot") {
      cfg.trigger = partial.trigger;
    }
    if (partial.hullTrigger === "always" || partial.hullTrigger === "shoot" || partial.hullTrigger === "off") {
      cfg.hullTrigger = partial.hullTrigger;
    }
    if (typeof partial.speed === "number" && isFinite(partial.speed)) {
      cfg.speed = Math.min(2.5, Math.max(0.4, partial.speed));
    }
    if (typeof partial.intensity === "number" && isFinite(partial.intensity)) {
      cfg.intensity = Math.min(1.8, Math.max(0.4, partial.intensity));
    }
    if (partial.presets && typeof partial.presets === "object") {
      for (const g of Object.keys(GROUP_META)) {
        const pk = partial.presets[g];
        if (pk && GROUP_META[g].presets[pk]) {
          cfg.presets[g] = pk;
        }
      }
    }
    saveConfig();
    if (!isEmbedded) refreshMenuState();
  }

  function setGroupPreset(group, presetKey) {
    if (GROUP_META[group] && GROUP_META[group].presets[presetKey]) {
      cfg.presets[group] = presetKey;
      saveConfig();
      if (!isEmbedded) refreshMenuState();
    }
  }

  const api = {
    init,
    GROUP_META,
    getConfig,
    setConfig,
    setGroupPreset,
    getStatus: selectedStatus,
  };
  window.__ptSkinAnim = api;
  if (typeof module !== "undefined" && module.exports) {
    module.exports = api;
  }

  if (!isEmbedded) {
    init({ embedded: false });
  }
})();

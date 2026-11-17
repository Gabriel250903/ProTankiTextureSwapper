(function () {
  'use strict';
  if (window.__postfx) return;
  const HOST_IPC_TOKEN_RAW = "__TS_IPC_TOKEN__";
  const HOST_IPC_TOKEN = HOST_IPC_TOKEN_RAW.startsWith("__TS_")
    ? ""
    : HOST_IPC_TOKEN_RAW;
  const reportLog = (level, msg) => {
    try {
      (console[level] || console.error)("[ProTanki Pack]", msg);
    } catch (e) {}
    if (!HOST_IPC_TOKEN) return;
    try {
      const text = String(msg || "").slice(0, 1800);
      fetch("http://127.0.0.1:47921/ptpack-log", {
        method: "POST",
        keepalive: true,
        headers: {
          "Content-Type": "application/json",
          "X-PTPack-Token": HOST_IPC_TOKEN,
        },
        body: JSON.stringify({
          level,
          msg: text,
        }),
      }).catch(() => {
        try {
          new Image().src =
            "http://127.0.0.1:47921/ptpack-log?level=" +
            encodeURIComponent(level) +
            "&msg=" +
            encodeURIComponent(text) +
            "&token=" +
            encodeURIComponent(HOST_IPC_TOKEN);
        } catch (e2) {}
      });
    } catch (e) {}
  };
  window.addEventListener("error", (e) => {
    const msg = String(e.message || "");
    if (msg.includes("ResizeObserver loop")) return;
    const loc = e.filename
      ? " @ " + e.filename.split("/").pop() + ":" + e.lineno + ":" + e.colno
      : "";
    reportLog("error", (e.error && e.error.stack) || msg + loc);
  });
  window.addEventListener("unhandledrejection", (e) => {
    const r = e.reason;
    reportLog("error", "UnhandledRejection: " + ((r && r.stack) || r));
  });
  window.__ptSkinAnimEmbedded = true;
  /*__TS_SKIN_ANIM_MODULE__*/
  const animations = window.__ptSkinAnim;
  if (animations && typeof animations.init === "function") {
    try {
      animations.init({
        embedded: true,
      });
    } catch (e) {
      reportLog(
        "error",
        "skin animations init failed: " + ((e && e.stack) || e),
      );
    }
  }




  // ---- settings (editable live from the console, e.g. __postfx.bloom.intensity = 1) ----
  const BLOOM = {
    enabled: true,
    threshold: 0.42,
    knee: 0.3,
    intensity: 1.3,
  };
  const DOF = {
    enabled: false,
    auto: true,
    focus: 1500,
    focusY: 0.22,
    speed: 6,
    strength: 1.0,
    maxR: 8,
    range: 0.12,
    samples: 24,
    mode: 0,
  };
  const GRADE = {
    enabled: false,
    expo: 0,
    contrast: 1,
    sat: 1,
    temp: 0,
    tint: 0,
    vig: 0,
    vigSize: 0.6,
  };
  const SHARP = {
    enabled: false,
    amount: 0.6,
    limit: 0.1,
  };
  const FOG = {
    enabled: false,
    visibility: 12000,
    start: 400,
    amount: 0.8,
    fall: 0,
    base: 0,
    color: 0,
    bright: 1,
    sky: 0.6,
  };
  const SUN = {
    enabled: false,
    yaw: 0,
    pitch: 0,
    intensity: 1,
    ambient: 1,
    warmth: 0,
    glow: 0,
    rays: 0,
    vec: null,
  }; // vec = direction towards the sun, filled in by the sun module
  const LIGHT = {
    enabled: false,
    onlyRain: true,
    gap: 15,
    flash: 1,
    bolts: true,
    thunder: true,
    vol: 0.5,
    now: 0,
  };
  const CAM = {
    enabled: true,
    strength: 0.5,
    maxLen: 0.05,
    samples: 16,
    mode: 0,
    dbg: [0, 0],
  };
  // type: 'off' | 'rain' | 'snow' (Alt+P cycles). World units here are roughly centimetres, +Y is up.
  const PRECIP = {
    type: "rain",
    count: 8000,
    alpha: 0.5,
    mood: 0.35, // mood = grey haze/darkening strength, 0 disables it
    box: [3600, 2400, 3600], // size of the particle volume that follows the camera
    windX: 500,
    windZ: 250, // horizontal drift (units/s)
    fall: 3800,
    len: 120,
    width: 1.0, // rain: fall speed, streak length, streak width
    snowFall: 180,
    snowSize: 6,
    sway: 120, // snow: fall speed, flake size, sideways drift
    gust: 0.4, // 0..1, how strongly the wind speed swells and fades
  };
  // Wet ground, used while precipitation is 'rain': puddles, ripples/splashes and reflections (needs the depth texture)
  const WET = {
    enabled: true,
    wet: 0.75, // overall darkening of everything that is wet
    puddle: 0.7, // puddle coverage 0..1
    refl: 0.95, // puddle reflection strength
    ripple: 0.9, // how often raindrops make ripples (0..1)
    maxDist: 9000, // beyond this distance nothing is wetted
    cell: 180, // ripple cell size in world units
    speed: 120, // ring expansion speed in world units per second
    hideTanks: (function () {
      try {
        return localStorage.getItem("postfx.hideTanks") !== "0";
      } catch (e) {
        return true;
      }
    })(), // keep puddles off tanks (uses the framebuffer alpha as a tank mask). Reload-persistent switch: localStorage.setItem('postfx.hideTanks','0')
    envSmooth: 0.25, // seconds: how slowly the reflected sky colour follows changes (higher = calmer)
    splash: true, // tanks that drive through puddles push ripples and foam (needs the tank mask and float render targets)
    splashAmt: 1, // strength of the ripples they make
    foam: 1.3, // amount of white spray/foam
    wake: 140, // speed of those waves in world units per second
    wakeLife: 2, // seconds until the waves die out
    contact: 38, // height window (world units) used to find where a tank touches the ground
    debug: 0, // 1 = show the tank mask, 2 = show the transparent-effect mask, 3 = show the splash waves
  };
  // Snow ground, used while precipitation is 'snow': snow cover that builds up on flat surfaces, plus tracks where tanks have driven (needs the depth texture)
  const SNOWG = {
    enabled: true,
    cover: 0.9, // how much of the flat ground the snow covers when fully built up (0..1)
    build: 360, // seconds of snowfall until the ground is fully covered (0 = at once)
    melt: 90, // seconds the snow needs to melt away when it is not snowing
    bright: 1.0, // brightness of the snow
    range: 14000, // beyond this distance nothing is covered
    tracks: true, // tanks press tracks into the snow
    trackDepth: 0.55, // how much snow a track pushes away (1 = bare ground); what is left (about 1 - this) is a thin pressed layer
    berm: 1.0, // how much snow is piled up along the sides of a track (0 = none)
    pattern: 1.0, // strength of the tread (cleat) pattern pressed into the tracks (0 = smooth)
    fade: 90, // seconds until fresh snowfall has filled a track in again (0 = never)
    contact: 38, // height tolerance (world units): a track only shows on a surface within about this much of the height where the tread pressed it
    debug: 0, // 1 = show the tank mask, 2 = show the track map
    level: 0,
    clearReq: 0, // runtime state: current build-up 0..1, and a request to wipe all tracks
  };




  // ======================= BLOOM (overlay canvas, screen-blended) =======================
  function installBloom(game) {
    const ggl = game.getContext("webgl2");
    const P = BLOOM;
    const ov = document.createElement("canvas");
    ov.id = "__bloom_overlay";
    ov.dataset.pfxOverlay = "1";
    ov.style.cssText =
      "position:fixed;pointer-events:none;mix-blend-mode:screen;";
    game.after(ov);
    const gl = ov.getContext("webgl2", {
      alpha: false,
      antialias: false,
      depth: false,
      stencil: false,
      powerPreference: "high-performance",
    });
    const VS = `#version 300 es
out vec2 uv; void main(){ vec2 p=vec2(float((gl_VertexID<<1)&2), float(gl_VertexID&2)); uv=p; gl_Position=vec4(p*2.0-1.0,0.0,1.0);}`;
    const mkProg = (fs) => {
      const p = gl.createProgram();
      for (const [t, s] of [
        [gl.VERTEX_SHADER, VS],
        [gl.FRAGMENT_SHADER, fs],
      ]) {
        const sh = gl.createShader(t);
        gl.shaderSource(sh, s);
        gl.compileShader(sh);
        if (!gl.getShaderParameter(sh, gl.COMPILE_STATUS))
          throw new Error(gl.getShaderInfoLog(sh));
        gl.attachShader(p, sh);
      }
      gl.linkProgram(p);
      if (!gl.getProgramParameter(p, gl.LINK_STATUS))
        throw new Error(gl.getProgramInfoLog(p));
      const u = {};
      const n = gl.getProgramParameter(p, gl.ACTIVE_UNIFORMS);
      for (let i = 0; i < n; i++) {
        const nm = gl.getActiveUniform(p, i).name;
        u[nm] = gl.getUniformLocation(p, nm);
      }
      return {
        p,
        u,
      };
    };
    const H =
      "#version 300 es\nprecision highp float; in vec2 uv; out vec4 o;\n";
    const bright = mkProg(
      H +
        `uniform sampler2D t; uniform vec2 texel; uniform float thr, knee;
void main(){ vec3 c=(texture(t,uv+texel*vec2(-1,-1)).rgb+texture(t,uv+texel*vec2(1,-1)).rgb+texture(t,uv+texel*vec2(-1,1)).rgb+texture(t,uv+texel*vec2(1,1)).rgb)*0.25;
 float l=max(c.r,max(c.g,c.b)); float s=clamp(l-thr+knee,0.0,2.0*knee); s=s*s/(4.0*knee+1e-4); float k=max(s,l-thr)/max(l,1e-4); o=vec4(c*k,1.0);}`,
    );
    const down = mkProg(
      H +
        `uniform sampler2D t; uniform vec2 texel;
void main(){ o=vec4((texture(t,uv+texel*vec2(-1,-1)).rgb+texture(t,uv+texel*vec2(1,-1)).rgb+texture(t,uv+texel*vec2(-1,1)).rgb+texture(t,uv+texel*vec2(1,1)).rgb)*0.25,1.0);}`,
    );
    const blur = mkProg(
      H +
        `uniform sampler2D t; uniform vec2 dir;
void main(){ vec3 c=texture(t,uv).rgb*0.2270270270; c+=(texture(t,uv+dir*1.3846153846).rgb+texture(t,uv-dir*1.3846153846).rgb)*0.3162162162; c+=(texture(t,uv+dir*3.2307692308).rgb+texture(t,uv-dir*3.2307692308).rgb)*0.0702702703; o=vec4(c,1.0);}`,
    );
    const comp = mkProg(
      H +
        `uniform sampler2D a, c; uniform float k;
void main(){ o=vec4((texture(a,uv).rgb*0.6+texture(c,uv).rgb*0.8)*k,1.0);}`,
    );
    const mkTex = (w, h) => {
      const t = gl.createTexture();
      gl.bindTexture(gl.TEXTURE_2D, t);
      gl.texImage2D(
        gl.TEXTURE_2D,
        0,
        gl.RGBA8,
        w,
        h,
        0,
        gl.RGBA,
        gl.UNSIGNED_BYTE,
        null,
      );
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
      return t;
    };
    const mkRT = (w, h) => {
      const tex = mkTex(w, h);
      const fb = gl.createFramebuffer();
      gl.bindFramebuffer(gl.FRAMEBUFFER, fb);
      gl.framebufferTexture2D(
        gl.FRAMEBUFFER,
        gl.COLOR_ATTACHMENT0,
        gl.TEXTURE_2D,
        tex,
        0,
      );
      return {
        tex,
        fb,
        w,
        h,
      };
    };
    const gameTex = mkTex(1, 1);
    let RT = null,
      gw = 0,
      gh = 0;
    const free = () => {
      if (!RT) return;
      for (const r of Object.values(RT)) {
        gl.deleteTexture(r.tex);
        gl.deleteFramebuffer(r.fb);
      }
      RT = null;
    };
    const setup = () => {
      free();
      gw = game.width;
      gh = game.height;
      ov.width = Math.max(1, gw >> 1);
      ov.height = Math.max(1, gh >> 1);
      const aw = Math.max(1, gw >> 2),
        ah = Math.max(1, gh >> 2),
        cw = Math.max(1, aw >> 1),
        ch = Math.max(1, ah >> 1);
      RT = {
        A: mkRT(aw, ah),
        B: mkRT(aw, ah),
        C: mkRT(cw, ch),
        D: mkRT(cw, ch),
      };
    };
    const pass = (pr, src, dst, setU) => {
      gl.bindFramebuffer(gl.FRAMEBUFFER, dst ? dst.fb : null);
      gl.viewport(0, 0, dst ? dst.w : ov.width, dst ? dst.h : ov.height);
      gl.useProgram(pr.p);
      gl.activeTexture(gl.TEXTURE0);
      gl.bindTexture(gl.TEXTURE_2D, src);
      if (pr.u.t) gl.uniform1i(pr.u.t, 0);
      setU && setU(pr.u);
      gl.drawArrays(gl.TRIANGLES, 0, 3);
    };
    const blurLevel = (X, Y, iters) => {
      for (let i = 0; i < iters; i++) {
        pass(blur, X.tex, Y, (u) => gl.uniform2f(u.dir, 1 / X.w, 0));
        pass(blur, Y.tex, X, (u) => gl.uniform2f(u.dir, 0, 1 / X.h));
      }
    };
    let lastRect = "";
    const render = () => {
      if (!P.enabled) {
        ov.style.display = "none";
        return;
      }
      ov.style.display = "";
      if (!RT || gw !== game.width || gh !== game.height) setup();
      const r = game.getBoundingClientRect();
      const key = [r.left, r.top, r.width, r.height].join();
      if (key !== lastRect) {
        lastRect = key;
        Object.assign(ov.style, {
          left: r.left + "px",
          top: r.top + "px",
          width: r.width + "px",
          height: r.height + "px",
        });
      }
      gl.bindTexture(gl.TEXTURE_2D, gameTex);
      gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, true);
      gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, game);
      pass(bright, gameTex, RT.A, (u) => {
        gl.uniform2f(u.texel, 1 / gw, 1 / gh);
        gl.uniform1f(u.thr, P.threshold);
        gl.uniform1f(u.knee, P.knee);
      });
      blurLevel(RT.A, RT.B, 1);
      pass(down, RT.A.tex, RT.C, (u) =>
        gl.uniform2f(u.texel, 1 / RT.A.w, 1 / RT.A.h),
      );
      blurLevel(RT.C, RT.D, 2);
      gl.useProgram(comp.p);
      gl.bindFramebuffer(gl.FRAMEBUFFER, null);
      gl.viewport(0, 0, ov.width, ov.height);
      gl.activeTexture(gl.TEXTURE0);
      gl.bindTexture(gl.TEXTURE_2D, RT.A.tex);
      gl.uniform1i(comp.u.a, 0);
      gl.activeTexture(gl.TEXTURE1);
      gl.bindTexture(gl.TEXTURE_2D, RT.C.tex);
      gl.uniform1i(comp.u.c, 1);
      gl.uniform1f(comp.u.k, P.intensity);
      gl.drawArrays(gl.TRIANGLES, 0, 3);
    };
    // run after the game has drawn: wrap rAF, render when the game issued draw calls
    let dirty = false;
    const names = [
      "drawElements",
      "drawArrays",
      "drawElementsInstanced",
      "drawArraysInstanced",
      "drawRangeElements",
    ];
    const undo = [];
    names.forEach((n) => {
      const own = Object.prototype.hasOwnProperty.call(ggl, n);
      const prev = ggl[n];
      ggl[n] = function () {
        dirty = true;
        return prev.apply(this, arguments);
      };
      undo.push(() => {
        if (own) ggl[n] = prev;
        else delete ggl[n];
      });
    });
    const origRAF = window.requestAnimationFrame;
    window.requestAnimationFrame = function (cb) {
      return origRAF.call(window, function (t) {
        try {
          cb(t);
        } finally {
          if (dirty) {
            dirty = false;
            try {
              render();
            } catch (e) {
              console.error("[postfx bloom]", e);
            }
          }
        }
      });
    };
    return function destroy() {
      window.requestAnimationFrame = origRAF;
      undo.forEach((f) => f());
      try {
        free();
      } catch (e) {}
      ov.remove();
    };
  }




  // ======================= CAMERA MOTION BLUR (in the game's own GL context) =======================
  function installCam(game) {
    const g = game.getContext("webgl2");
    const proto = WebGL2RenderingContext.prototype;
    const R = new Proxy(
      {},
      {
        get:
          (_, n) =>
          (...a) =>
            proto[n].apply(g, a),
      },
    ); // calls that bypass our own hooks
    const P = CAM;
    const st = {
      passes: 0,
      err: null,
      noCap: 0,
      noDepth: 0,
    };
    const mul = (a, b) => {
      const r = new Float64Array(16);
      for (let c = 0; c < 4; c++)
        for (let rw = 0; rw < 4; rw++) {
          let s = 0;
          for (let k = 0; k < 4; k++) s += a[k * 4 + rw] * b[c * 4 + k];
          r[c * 4 + rw] = s;
        }
      return r;
    };
    const inv = (m) => {
      const i = new Float64Array(16);
      i[0] =
        m[5] * m[10] * m[15] -
        m[5] * m[11] * m[14] -
        m[9] * m[6] * m[15] +
        m[9] * m[7] * m[14] +
        m[13] * m[6] * m[11] -
        m[13] * m[7] * m[10];
      i[4] =
        -m[4] * m[10] * m[15] +
        m[4] * m[11] * m[14] +
        m[8] * m[6] * m[15] -
        m[8] * m[7] * m[14] -
        m[12] * m[6] * m[11] +
        m[12] * m[7] * m[10];
      i[8] =
        m[4] * m[9] * m[15] -
        m[4] * m[11] * m[13] -
        m[8] * m[5] * m[15] +
        m[8] * m[7] * m[13] +
        m[12] * m[5] * m[11] -
        m[12] * m[7] * m[9];
      i[12] =
        -m[4] * m[9] * m[14] +
        m[4] * m[10] * m[13] +
        m[8] * m[5] * m[14] -
        m[8] * m[6] * m[13] -
        m[12] * m[5] * m[10] +
        m[12] * m[6] * m[9];
      i[1] =
        -m[1] * m[10] * m[15] +
        m[1] * m[11] * m[14] +
        m[9] * m[2] * m[15] -
        m[9] * m[3] * m[14] -
        m[13] * m[2] * m[11] +
        m[13] * m[3] * m[10];
      i[5] =
        m[0] * m[10] * m[15] -
        m[0] * m[11] * m[14] -
        m[8] * m[2] * m[15] +
        m[8] * m[3] * m[14] +
        m[12] * m[2] * m[11] -
        m[12] * m[3] * m[10];
      i[9] =
        -m[0] * m[9] * m[15] +
        m[0] * m[11] * m[13] +
        m[8] * m[1] * m[15] -
        m[8] * m[3] * m[13] -
        m[12] * m[1] * m[11] +
        m[12] * m[3] * m[9];
      i[13] =
        m[0] * m[9] * m[14] -
        m[0] * m[10] * m[13] -
        m[8] * m[1] * m[14] +
        m[8] * m[2] * m[13] +
        m[12] * m[1] * m[10] -
        m[12] * m[2] * m[9];
      i[2] =
        m[1] * m[6] * m[15] -
        m[1] * m[7] * m[14] -
        m[5] * m[2] * m[15] +
        m[5] * m[3] * m[14] +
        m[13] * m[2] * m[7] -
        m[13] * m[3] * m[6];
      i[6] =
        -m[0] * m[6] * m[15] +
        m[0] * m[7] * m[14] +
        m[4] * m[2] * m[15] -
        m[4] * m[3] * m[14] -
        m[12] * m[2] * m[7] +
        m[12] * m[3] * m[6];
      i[10] =
        m[0] * m[5] * m[15] -
        m[0] * m[7] * m[13] -
        m[4] * m[1] * m[15] +
        m[4] * m[3] * m[13] +
        m[12] * m[1] * m[7] -
        m[12] * m[3] * m[5];
      i[14] =
        -m[0] * m[5] * m[14] +
        m[0] * m[6] * m[13] +
        m[4] * m[1] * m[14] -
        m[4] * m[2] * m[13] -
        m[12] * m[1] * m[6] +
        m[12] * m[2] * m[5];
      i[3] =
        -m[1] * m[6] * m[11] +
        m[1] * m[7] * m[10] +
        m[5] * m[2] * m[11] -
        m[5] * m[3] * m[10] -
        m[9] * m[2] * m[7] +
        m[9] * m[3] * m[6];
      i[7] =
        m[0] * m[6] * m[11] -
        m[0] * m[7] * m[10] -
        m[4] * m[2] * m[11] +
        m[4] * m[3] * m[10] +
        m[8] * m[2] * m[7] -
        m[8] * m[3] * m[6];
      i[11] =
        -m[0] * m[5] * m[11] +
        m[0] * m[7] * m[9] +
        m[4] * m[1] * m[11] -
        m[4] * m[3] * m[9] -
        m[8] * m[1] * m[7] +
        m[8] * m[3] * m[5];
      i[15] =
        m[0] * m[5] * m[10] -
        m[0] * m[6] * m[9] -
        m[4] * m[1] * m[10] +
        m[4] * m[2] * m[9] +
        m[8] * m[1] * m[6] -
        m[8] * m[2] * m[5];
      let det = m[0] * i[0] + m[1] * i[4] + m[2] * i[8] + m[3] * i[12];
      if (Math.abs(det) < 1e-30) return null;
      det = 1 / det;
      for (let k = 0; k < 16; k++) i[k] *= det;
      return i;
    };
    let prog = null,
      U = null,
      vao = null,
      colTex = null,
      samp = null,
      size = [0, 0],
      prevVP = null;
    const build = (w, h) => {
      const at = R.getParameter(g.ACTIVE_TEXTURE);
      R.activeTexture(g.TEXTURE0);
      const tb = R.getParameter(g.TEXTURE_BINDING_2D);
      try {
        if (!prog) {
          const vs = `#version 300 es
out vec2 uv; void main(){ vec2 p=vec2(float((gl_VertexID<<1)&2), float(gl_VertexID&2)); uv=p; gl_Position=vec4(p*2.0-1.0,0.0,1.0);}`;
          const fs = `#version 300 es
precision highp float; precision highp sampler2D; in vec2 uv; out vec4 o;
uniform sampler2D col, dep; uniform mat4 rp; uniform float strength, maxLen; uniform vec2 dbg; uniform int N, mode;
void main(){ float d=texture(dep,uv).r; vec4 pc=rp*vec4(uv*2.0-1.0, d*2.0-1.0, 1.0);
 vec2 v=vec2(0.0); if(pc.w>1e-6){ v=(uv-(pc.xy/pc.w*0.5+0.5)); } v=(v+dbg)*strength; float l=length(v); if(l>maxLen) v*=maxLen/l;
 if(mode==1){ o=vec4(vec3(pow(d,64.0)),1.0); return; } if(mode==2){ o=vec4(abs(v)*40.0,0.0,1.0); return; }
 vec3 c=vec3(0.0); for(int i=0;i<64;i++){ if(i>=N) break; float t=(float(i)+0.5)/float(N)-0.5; c+=texture(col,uv+v*t).rgb; } o=vec4(c/float(N),1.0);}`;
          const p = R.createProgram();
          for (const [t, s] of [
            [g.VERTEX_SHADER, vs],
            [g.FRAGMENT_SHADER, fs],
          ]) {
            const sh = R.createShader(t);
            R.shaderSource(sh, s);
            R.compileShader(sh);
            if (!R.getShaderParameter(sh, g.COMPILE_STATUS))
              throw new Error(R.getShaderInfoLog(sh));
            R.attachShader(p, sh);
          }
          R.linkProgram(p);
          if (!R.getProgramParameter(p, g.LINK_STATUS))
            throw new Error(R.getProgramInfoLog(p));
          U = {};
          for (const n of [
            "col",
            "dep",
            "rp",
            "strength",
            "maxLen",
            "dbg",
            "N",
            "mode",
          ])
            U[n] = R.getUniformLocation(p, n);
          prog = p;
          vao = R.createVertexArray();
          samp = R.createSampler();
          R.samplerParameteri(samp, g.TEXTURE_MIN_FILTER, g.NEAREST);
          R.samplerParameteri(samp, g.TEXTURE_MAG_FILTER, g.NEAREST);
          R.samplerParameteri(samp, g.TEXTURE_WRAP_S, g.CLAMP_TO_EDGE);
          R.samplerParameteri(samp, g.TEXTURE_WRAP_T, g.CLAMP_TO_EDGE);
          R.samplerParameteri(samp, g.TEXTURE_COMPARE_MODE, g.NONE);
        }
        if (colTex) R.deleteTexture(colTex);
        colTex = R.createTexture();
        R.bindTexture(g.TEXTURE_2D, colTex);
        R.texParameteri(g.TEXTURE_2D, g.TEXTURE_MIN_FILTER, g.LINEAR);
        R.texParameteri(g.TEXTURE_2D, g.TEXTURE_MAG_FILTER, g.LINEAR);
        R.texParameteri(g.TEXTURE_2D, g.TEXTURE_WRAP_S, g.CLAMP_TO_EDGE);
        R.texParameteri(g.TEXTURE_2D, g.TEXTURE_WRAP_T, g.CLAMP_TO_EDGE);
        R.texImage2D(
          g.TEXTURE_2D,
          0,
          g.RGBA8,
          w,
          h,
          0,
          g.RGBA,
          g.UNSIGNED_BYTE,
          null,
        );
        size = [w, h];
        return true;
      } catch (e) {
        st.err = String(e);
        return false;
      } finally {
        R.bindTexture(g.TEXTURE_2D, tb);
        R.activeTexture(at);
      }
    };
    const hooks = [];
    const hook = (n, fn) => {
      const own = Object.prototype.hasOwnProperty.call(g, n);
      const prev = g[n];
      g[n] = fn(prev);
      hooks.push(() => {
        if (own) g[n] = prev;
        else delete g[n];
      });
    };
    let curDraw = null,
      curRead = null,
      curProg = null;
    const cap = new Map(),
      capP = new Map();
    const pinfo = new WeakMap();
    const depthOf = new WeakMap();
    hook(
      "bindFramebuffer",
      (prev) =>
        function (t, fb) {
          if (t === g.FRAMEBUFFER) {
            curDraw = curRead = fb;
          } else if (t === g.DRAW_FRAMEBUFFER) curDraw = fb;
          else if (t === g.READ_FRAMEBUFFER) curRead = fb;
          return prev.apply(this, arguments);
        },
    );
    hook(
      "useProgram",
      (prev) =>
        function (p) {
          curProg = p;
          return prev.apply(this, arguments);
        },
    );
    // track BLEND: the first blended draw into the main framebuffer marks where the opaque scene ends and transparent effects begin
    let blendOn = R.isEnabled(g.BLEND),
      preState = 0; // preState: 0 = not captured this frame, 1 = captured, 2 = failed
    hook(
      "enable",
      (prev) =>
        function (c) {
          if (c === g.BLEND) blendOn = true;
          return prev.apply(this, arguments);
        },
    );
    hook(
      "disable",
      (prev) =>
        function (c) {
          if (c === g.BLEND) blendOn = false;
          return prev.apply(this, arguments);
        },
    );
    // --- tank mask: while rain is on, the main framebuffer's ALPHA channel is cleared to 0 and only tank draws may write it (alpha 1).
    // The wet pass reads it to keep puddles off tanks, then alpha is set back to 1 before the frame is shown.
    let mainFb = null,
      alphaFrame = false;
    let gmBits = 15,
      appliedBits = 15;
    {
      const m = [true, true, true, true].map(
        (_, k) => !!R.getParameter(g.COLOR_WRITEMASK)[k],
      );
      gmBits = appliedBits =
        (m[0] ? 1 : 0) | (m[1] ? 2 : 0) | (m[2] ? 4 : 0) | (m[3] ? 8 : 0);
    }
    let ccv = R.getParameter(g.COLOR_CLEAR_VALUE);
    hook(
      "clearColor",
      (prev) =>
        function (r, gg, b, a) {
          ccv = [r, gg, b, a];
          return prev.apply(this, arguments);
        },
    );
    // blend state is tracked from the game's own calls (asking the GPU for it stalls the pipeline)
    const bs = {
      f: [
        R.getParameter(g.BLEND_SRC_RGB),
        R.getParameter(g.BLEND_DST_RGB),
        R.getParameter(g.BLEND_SRC_ALPHA),
        R.getParameter(g.BLEND_DST_ALPHA),
      ],
      e: [
        R.getParameter(g.BLEND_EQUATION_RGB),
        R.getParameter(g.BLEND_EQUATION_ALPHA),
      ],
    };
    hook(
      "blendFunc",
      (prev) =>
        function (a, b) {
          bs.f = [a, b, a, b];
          return prev.apply(this, arguments);
        },
    );
    hook(
      "blendFuncSeparate",
      (prev) =>
        function (a, b, c, d) {
          bs.f = [a, b, c, d];
          return prev.apply(this, arguments);
        },
    );
    hook(
      "blendEquation",
      (prev) =>
        function (m) {
          bs.e = [m, m];
          return prev.apply(this, arguments);
        },
    );
    hook(
      "blendEquationSeparate",
      (prev) =>
        function (a, b) {
          bs.e = [a, b];
          return prev.apply(this, arguments);
        },
    );
    const bitsToMask = (b) =>
      R.colorMask(!!(b & 1), !!(b & 2), !!(b & 4), !!(b & 8));
    const maskOn = () =>
      !!(
        mainFb &&
        ((PRECIP.type === "rain" &&
          WET.enabled &&
          (WET.hideTanks || WET.splash)) ||
          (PRECIP.type === "snow" && SNOWG.enabled))
      );
    const groundOn = () =>
      (WET.enabled && PRECIP.type === "rain") ||
      (SNOWG.enabled && PRECIP.type === "snow");
    const tankProg = new WeakMap();
    const isTankProg = (p) => {
      let v = tankProg.get(p);
      if (v === undefined) {
        v = false;
        try {
          const n = R.getProgramParameter(p, g.ACTIVE_UNIFORMS);
          for (let i = 0; i < n; i++)
            if (/tank/i.test(R.getActiveUniform(p, i).name)) {
              v = true;
              break;
            }
        } catch (e) {}
        tankProg.set(p, v);
      }
      return v;
    };
    hook(
      "colorMask",
      (prev) =>
        function (a, b, c, d) {
          gmBits = (a ? 1 : 0) | (b ? 2 : 0) | (c ? 4 : 0) | (d ? 8 : 0);
          appliedBits = gmBits;
          return prev.apply(this, arguments);
        },
    );
    // put the hardware colour mask back to what the game believes it is (clears honour the mask, so a stale
    // alpha-off mask left over from the tank mask would stop shadow/depth targets from being cleared)
    const syncMask = () => {
      if (appliedBits !== gmBits) {
        bitsToMask(gmBits);
        appliedBits = gmBits;
      }
    };
    hook(
      "clear",
      (prev) =>
        function (m) {
          syncMask();
          const r = prev.apply(this, arguments);
          if (
            m & g.COLOR_BUFFER_BIT &&
            curDraw &&
            curDraw === mainFb &&
            maskOn()
          ) {
            const cc = ccv;
            R.colorMask(false, false, false, true);
            R.clearColor(0, 0, 0, 0);
            R.clear(g.COLOR_BUFFER_BIT);
            R.clearColor(cc[0], cc[1], cc[2], cc[3]);
            bitsToMask(appliedBits);
            alphaFrame = true;
          }
          return r;
        },
    );
    const fixAlpha = () => {
      // make the whole frame opaque again
      const cc = ccv;
      R.colorMask(false, false, false, true);
      R.clearColor(0, 0, 0, 1);
      R.clear(g.COLOR_BUFFER_BIT);
      R.clearColor(cc[0], cc[1], cc[2], cc[3]);
      bitsToMask(appliedBits);
    };
    // remember the first view/projection matrices used per framebuffer each frame
    const onDraw = (nm, args) => {
      if (
        nm === "drawArrays" &&
        mainFb &&
        curDraw === mainFb &&
        curProg &&
        groundOn() &&
        isTankProg(curProg)
      ) {
        try {
          ground.noteTank(curProg, args[1], args[2]);
        } catch (e) {}
      }
      if (
        preState === 0 &&
        blendOn &&
        mainFb &&
        curDraw === mainFb &&
        groundOn()
      )
        capturePre();
      if (mainFb) {
        const eff =
          curDraw === mainFb && maskOn()
            ? (gmBits & 7) |
              (gmBits & 8 && curProg && isTankProg(curProg) ? 8 : 0)
            : gmBits;
        if (eff !== appliedBits) {
          bitsToMask(eff);
          appliedBits = eff;
        }
      }
      if (curDraw && curProg && !cap.has(curDraw)) {
        let pi = pinfo.get(curProg);
        if (!pi) {
          pi = {
            v: R.getUniformLocation(curProg, "viewMatrix"),
            p: R.getUniformLocation(curProg, "projectionMatrix"),
          };
          pinfo.set(curProg, pi);
        }
        if (pi.p && !capP.has(curDraw))
          capP.set(curDraw, R.getUniform(curProg, pi.p));
        if (pi.v && pi.p)
          cap.set(curDraw, {
            view: R.getUniform(curProg, pi.v),
            proj: R.getUniform(curProg, pi.p),
          });
      }
    };
    [
      "drawElements",
      "drawArrays",
      "drawElementsInstanced",
      "drawArraysInstanced",
      "drawRangeElements",
    ].forEach((n) =>
      hook(
        n,
        (prev) =>
          function () {
            onDraw(n, arguments);
            return prev.apply(this, arguments);
          },
      ),
    );
    // the game's own full-res perspective depth pre-pass (a framebuffer with a depth TEXTURE)
    const findDepth = () => {
      for (const [fb, pm] of capP) {
        if (fb === curRead || Math.abs(pm[11] + 1) > 1e-3) continue;
        let d = depthOf.get(fb);
        if (d === undefined) {
          const rb = curRead;
          R.bindFramebuffer(g.READ_FRAMEBUFFER, fb);
          const ty = R.getFramebufferAttachmentParameter(
            g.READ_FRAMEBUFFER,
            g.DEPTH_ATTACHMENT,
            g.FRAMEBUFFER_ATTACHMENT_OBJECT_TYPE,
          );
          d =
            ty === g.TEXTURE
              ? R.getFramebufferAttachmentParameter(
                  g.READ_FRAMEBUFFER,
                  g.DEPTH_ATTACHMENT,
                  g.FRAMEBUFFER_ATTACHMENT_OBJECT_NAME,
                )
              : null;
          R.bindFramebuffer(g.READ_FRAMEBUFFER, rb);
          depthOf.set(fb, d);
        }
        if (d) return d;
      }
      return null;
    };
    const runPass = (w, h, cp, depth) => {
      const S = {
        read: curRead,
        draw: curDraw,
        prog: R.getParameter(g.CURRENT_PROGRAM),
        vao: R.getParameter(g.VERTEX_ARRAY_BINDING),
        at: R.getParameter(g.ACTIVE_TEXTURE),
        vp: R.getParameter(g.VIEWPORT),
        sc: R.isEnabled(g.SCISSOR_TEST),
        dt: R.isEnabled(g.DEPTH_TEST),
        bl: R.isEnabled(g.BLEND),
        cf: R.isEnabled(g.CULL_FACE),
        stn: R.isEnabled(g.STENCIL_TEST),
        cm: [
          !!(appliedBits & 1),
          !!(appliedBits & 2),
          !!(appliedBits & 4),
          !!(appliedBits & 8),
        ],
      };
      R.activeTexture(g.TEXTURE0);
      S.t0 = R.getParameter(g.TEXTURE_BINDING_2D);
      S.s0 = R.getParameter(g.SAMPLER_BINDING);
      R.activeTexture(g.TEXTURE1);
      S.t1 = R.getParameter(g.TEXTURE_BINDING_2D);
      S.s1 = R.getParameter(g.SAMPLER_BINDING);
      try {
        R.bindFramebuffer(g.FRAMEBUFFER, null);
        R.activeTexture(g.TEXTURE0);
        R.bindSampler(0, null);
        R.bindTexture(g.TEXTURE_2D, colTex);
        R.copyTexSubImage2D(g.TEXTURE_2D, 0, 0, 0, 0, 0, w, h);
        R.activeTexture(g.TEXTURE1);
        R.bindTexture(g.TEXTURE_2D, depth);
        R.bindSampler(1, samp);
        R.disable(g.SCISSOR_TEST);
        R.disable(g.DEPTH_TEST);
        R.disable(g.BLEND);
        R.disable(g.CULL_FACE);
        R.disable(g.STENCIL_TEST);
        R.colorMask(true, true, true, true);
        R.viewport(0, 0, w, h);
        R.useProgram(prog);
        R.bindVertexArray(vao);
        R.uniform1i(U.col, 0);
        R.uniform1i(U.dep, 1);
        R.uniformMatrix4fv(U.rp, false, new Float32Array(cp));
        R.uniform1f(U.strength, P.strength);
        R.uniform1f(U.maxLen, P.maxLen);
        R.uniform2f(U.dbg, P.dbg[0], P.dbg[1]);
        R.uniform1i(U.N, P.samples | 0);
        R.uniform1i(U.mode, P.mode | 0);
        R.drawArrays(g.TRIANGLES, 0, 3);
        st.passes++;
      } finally {
        // put every piece of GL state back exactly as the game left it
        R.activeTexture(g.TEXTURE0);
        R.bindTexture(g.TEXTURE_2D, S.t0);
        R.bindSampler(0, S.s0);
        R.activeTexture(g.TEXTURE1);
        R.bindTexture(g.TEXTURE_2D, S.t1);
        R.bindSampler(1, S.s1);
        R.activeTexture(S.at);
        R.bindVertexArray(S.vao);
        R.useProgram(S.prog);
        R.viewport(S.vp[0], S.vp[1], S.vp[2], S.vp[3]);
        (S.sc ? R.enable : R.disable)(g.SCISSOR_TEST);
        (S.dt ? R.enable : R.disable)(g.DEPTH_TEST);
        (S.bl ? R.enable : R.disable)(g.BLEND);
        (S.cf ? R.enable : R.disable)(g.CULL_FACE);
        (S.stn ? R.enable : R.disable)(g.STENCIL_TEST);
        R.colorMask(S.cm[0], S.cm[1], S.cm[2], S.cm[3]);
        R.bindFramebuffer(g.READ_FRAMEBUFFER, S.read);
        R.bindFramebuffer(g.DRAW_FRAMEBUFFER, S.draw);
      }
    };
    // ---------- wet ground: puddles, ripples/splashes, reflections. Rebuilds positions and normals from the depth texture ----------
    const WFS = `#version 300 es
precision highp float; precision highp int; precision highp sampler2D;
uniform sampler2D col, dep, pre, env, sim; uniform mat4 invVR, VR; uniform vec3 camPos; uniform vec2 res, AB;
uniform float tm, wet, puddle, refl, ripple, maxDist, cell, speed, maskOn, dbgMode, simOn, simExt, simN, splAmt, foamAmt;
out vec4 o;
float hash(vec2 p){ vec3 p3=fract(vec3(p.xyx)*.1031); p3+=dot(p3,p3.yzx+33.33); return fract((p3.x+p3.y)*p3.z); }
float vn(vec2 p){ vec2 i=floor(p), f=fract(p); f=f*f*(3.0-2.0*f); return mix(mix(hash(i),hash(i+vec2(1.0,0.0)),f.x), mix(hash(i+vec2(0.0,1.0)),hash(i+vec2(1.0,1.0)),f.x), f.y); }
float fbm(vec2 p){ float a=0.5,s=0.0; for(int i=0;i<4;i++){ s+=a*vn(p); p=p*2.03+11.7; a*=0.5; } return s; }
float lin(float z){ return AB.y/(z*2.0-1.0+AB.x); }
vec3 posAt(vec2 uv){ float d=texture(dep,uv).r; vec4 w=invVR*vec4(uv*2.0-1.0,d*2.0-1.0,1.0); return w.xyz/w.w; }
// stable sky colour by elevation (y of the reflected direction). 'env' is a tiny table that is rebuilt from the visible sky every frame
// and smoothed over time, so it never depends on what happens to be on screen at this very moment
vec3 envSky(float y){ return texture(env, vec2(sqrt(clamp(y,0.0,1.0)), 0.5)).rgb; }
// screen-space reflection: march the reflected ray in equal screen-space steps, then refine the hit by bisection
// returns (colour, confidence). Rays that cannot be resolved from the frame (leaving through the top, sides or bottom of the screen)
// get the stable environment colour instead of whatever is on screen at the edge, which used to smear into streaks.
vec4 ssr(vec3 P, vec3 R, float jit){
  vec3 sky=envSky(R.y);
  vec4 c0=VR*vec4(P+R*10.0,1.0);
  vec4 c1=VR*vec4(P+R*7000.0,1.0);
  if(c0.w<40.0) return vec4(sky,1.0);
  if(c1.w<40.0){ float tt=(c0.w-40.0)/(c0.w-c1.w); c1=c0+(c1-c0)*tt; }
  vec2 s0=c0.xy/c0.w, s1=c1.xy/c1.w; float k0=1.0/c0.w, k1=1.0/c1.w;
  vec2 ds=s1-s0;
  float ux=1e9, uy=1e9;
  if(ds.x>1e-5) ux=(1.0-s0.x)/ds.x; else if(ds.x<-1e-5) ux=(-1.0-s0.x)/ds.x;
  if(ds.y>1e-5) uy=(1.0-s0.y)/ds.y; else if(ds.y<-1e-5) uy=(-1.0-s0.y)/ds.y;
  float um=max(min(1.0,min(ux,uy)),0.0);
  bool topExit = ds.y>1e-5 && uy<=ux && uy<1.0;
  bool skyHit = topExit || (um>=1.0 && R.y>0.15);
  // steep upward rays are almost certainly looking at sky whichever edge they leave through; blend so there is no seam.
  // Anything else that leaves the screen sees unknown geometry: use a dimmed environment colour.
  float wsky=skyHit ? 1.0 : smoothstep(0.15,0.55,R.y);
  vec4 miss=vec4(sky*mix(0.6,1.0,wsky), mix(0.75,1.0,wsky));
  float uPrev=0.0;
  for(int i=0;i<48;i++){
    float u=um*(float(i)+0.5+jit*0.9)/48.0;
    vec2 q=(s0+ds*u)*0.5+0.5;
    float wr=1.0/mix(k0,k1,u);
    float diff=wr-lin(textureLod(dep,q,0.0).r);
    if(diff>0.0 && diff<max(40.0,wr*0.06)){
      float lo=uPrev, hi=u;
      for(int j=0;j<6;j++){
        float m=0.5*(lo+hi); vec2 qm=(s0+ds*m)*0.5+0.5;
        float dm=1.0/mix(k0,k1,m)-lin(textureLod(dep,qm,0.0).r);
        if(dm>0.0) hi=m; else lo=m;
      }
      vec2 qh=(s0+ds*hi)*0.5+0.5;
      float e=smoothstep(0.0,0.14,min(min(qh.x,1.0-qh.x),min(qh.y,1.0-qh.y)));
      return vec4(mix(miss.rgb,textureLod(col,qh,0.0).rgb,e), mix(miss.a,1.0,e));
    }
    uPrev=u;
  }
  return miss;
}
// returns (height-gradient x, height-gradient z, splash flash)
vec3 ripples(vec2 p, float t){
  vec2 g=vec2(0.0); float sp=0.0; vec2 c0=floor(p/cell);
  for(int j=-1;j<=1;j++) for(int i=-1;i<=1;i++){
    vec2 c=c0+vec2(float(i),float(j));
    float r1=hash(c), r2=hash(c+7.7), r3=hash(c+13.1);
    float period=0.7+r3*0.9;
    float x=t/period+r1*7.0; float cyc=floor(x); float ph=fract(x);
    if(hash(c+cyc*3.7+29.0)>ripple) continue;
    vec2 ctr=(c+vec2(r1,r2)*0.8+0.1)*cell;
    vec2 dv=p-ctr; float d=length(dv); float age=ph*period;
    float R=age*speed; float w=5.0+age*14.0; float xx=d-R; float k=0.35;
    float env=exp(-xx*xx/(2.0*w*w))*(1.0-ph)*(1.0-ph);
    float dr=env*(-xx/(w*w)*cos(k*xx)-k*sin(k*xx));
    g+=dv/(d+1e-3)*dr*60.0;
    if(age<0.1) sp+=(1.0-age/0.1)*smoothstep(9.0,0.0,d);
  }
  return vec3(g,sp);
}
void main(){
  vec2 uv=gl_FragCoord.xy/res;
  vec4 b4=texture(col,uv); vec3 fin=b4.rgb; vec3 base=texture(pre,uv).rgb;   // fin = final frame, base = opaque scene before any blended draw
  float tank= maskOn>0.5 ? smoothstep(0.2,0.8,b4.a) : 0.0;
  float addl=max(max(fin.r-base.r,fin.g-base.g),fin.b-base.b);
  float cover=smoothstep(0.04,0.4,addl);   // pixels brightened by transparent effects (flames, sparks...) after the opaque scene was drawn
  if(dbgMode>0.5 && dbgMode<1.5){ o=vec4(vec3(tank),1.0); return; }
  if(dbgMode>1.5&&dbgMode<2.5){ o=vec4(vec3(cover),1.0); return; }
  float d0=texture(dep,uv).r;
  vec3 P=posAt(uv); float dist=length(P);
  if(d0>=0.99999 || dist>maxDist*1.2){ o=vec4(fin,1.0); return; }
  vec2 px=1.0/res;
  vec3 Pl=posAt(uv-vec2(px.x,0.0)), Pr=posAt(uv+vec2(px.x,0.0)), Pd=posAt(uv-vec2(0.0,px.y)), Pu=posAt(uv+vec2(0.0,px.y));
  vec3 dx= abs(length(Pr)-dist) < abs(dist-length(Pl)) ? Pr-P : P-Pl;
  vec3 dy= abs(length(Pu)-dist) < abs(dist-length(Pd)) ? Pu-P : P-Pd;
  vec3 N=normalize(cross(dx,dy)); if(dot(N,-P)<0.0) N=-N;
  float isFlat=smoothstep(0.92,0.985,N.y);
  vec3 V=normalize(P);
  vec2 wp=(P+camPos).xz;
  if(dbgMode>2.5&&simOn>0.5){ vec4 sm=texture(sim,fract(wp/simExt)); o=vec4(0.5+sm.r*2.0,sm.b,0.5-sm.r*2.0,1.0); return; }
  float fade=1.0-smoothstep(maxDist*0.55,maxDist,dist);
  float nn=fbm(wp*0.0006)*0.7+fbm(wp*0.0023+31.0)*0.3;
  float t0=mix(0.66,0.40,puddle);
  float pm=smoothstep(t0,t0+0.05,nn)*isFlat*fade*(1.0-tank);
  float rim=pm*(1.0-pm)*4.0;
  float rfade=1.0-smoothstep(1400.0,3800.0,dist);
  vec3 rp=vec3(0.0);
  float rAmt=isFlat*rfade*(0.2+0.8*pm)*(1.0-tank);
  if(rAmt>0.01) rp=ripples(wp,tm)*rAmt;
  float foamV=0.0;   // waves and foam left by tanks that drove through the puddle
  vec2 wv=vec2(0.0);
  if(simOn>0.5 && pm>0.02){
    vec2 suv=fract(wp/simExt); float e=1.0/simN;
    float sw=1.0-smoothstep(0.78,0.96,max(abs(P.x),abs(P.z))/(simExt*0.5));
    float hx=texture(sim,suv+vec2(e,0.0)).r-texture(sim,suv-vec2(e,0.0)).r;
    float hz=texture(sim,suv+vec2(0.0,e)).r-texture(sim,suv-vec2(0.0,e)).r;
    wv=vec2(hx,hz)*(22.0*splAmt)*sw*pm; wv*=min(1.0,20.0/max(length(wv),1e-4));   // tilt only the reflection, capped, so big waves do not turn the puddle into a mirror
    float spk=hash(floor(wp*0.45)+vec2(floor(tm*18.0)*17.3));
    foamV=smoothstep(0.25,1.0,texture(sim,suv).b*(0.5+spk))*foamAmt*sw*pm;
  }
  vec3 Np=normalize(vec3(-(rp.x+wv.x)*0.012,1.0,-(rp.y+wv.y)*0.012));
  vec3 c=base;
  c*=1.0-0.22*wet*fade*(1.0-tank);
  vec4 r=vec4(0.0);
  if(pm>0.02) r=ssr(P,reflect(V,Np),hash(gl_FragCoord.xy)-0.5);
  float dk=mix(0.5,1.0,r.a);   // puddle darkening only where a reflection actually fills it in
  c*=1.0-0.35*pm*dk; c*=1.0-0.25*rim*dk;
  float ndv=max(dot(Np,-V),0.0);
  float F=clamp(mix(0.22,1.0,pow(1.0-ndv,3.0))*refl,0.0,1.0);
  F*=1.0-0.45*clamp(length(wv)/20.0,0.0,1.0);   // disturbed water reflects less
  if(pm>0.02){ c=mix(c,r.rgb,F*pm*r.a); }
  c+=vec3(0.9,0.95,1.0)*(clamp(length(rp.xy)*0.015,0.0,1.0)*0.18*pm + rp.z*0.35*isFlat*rfade*(1.0-tank));
  { float flc=dot(c,vec3(0.299,0.587,0.114));   // foam: lit by the scene, broken up, and see-through
    float fh1=hash(floor(wp*0.8)+7.0), fh2=hash(floor(wp*0.22)+3.0);
    float fa=clamp(clamp(foamV,0.0,1.0)*(0.35+0.9*fh1)*(0.65+0.7*fh2),0.0,0.7);
    vec3 fc=mix(vec3(0.80,0.88,0.96)*clamp(0.16+flc*1.7,0.0,0.8),c*1.3+0.06,0.35);
    c=mix(c,fc,fa); }
  o=vec4(fin+(c-base)*(1.0-cover),1.0);   // the wet look is applied to the opaque scene only; effects drawn on top stay on top
}`;
    // ---------- copy of the opaque scene, taken right before the first blended draw (flames, smoke, sparks are drawn after it) ----------
    let preTex = null,
      preFb = null,
      preSize = [0, 0];
    const ensurePre = (w, h) => {
      if (preTex && preSize[0] === w && preSize[1] === h) return true;
      const at = R.getParameter(g.ACTIVE_TEXTURE);
      R.activeTexture(g.TEXTURE0);
      const tb = R.getParameter(g.TEXTURE_BINDING_2D);
      const fbb = R.getParameter(g.DRAW_FRAMEBUFFER_BINDING);
      try {
        if (preTex) R.deleteTexture(preTex);
        if (!preFb) preFb = R.createFramebuffer();
        preTex = R.createTexture();
        R.bindTexture(g.TEXTURE_2D, preTex);
        R.texParameteri(g.TEXTURE_2D, g.TEXTURE_MIN_FILTER, g.LINEAR);
        R.texParameteri(g.TEXTURE_2D, g.TEXTURE_MAG_FILTER, g.LINEAR);
        R.texParameteri(g.TEXTURE_2D, g.TEXTURE_WRAP_S, g.CLAMP_TO_EDGE);
        R.texParameteri(g.TEXTURE_2D, g.TEXTURE_WRAP_T, g.CLAMP_TO_EDGE);
        R.texImage2D(
          g.TEXTURE_2D,
          0,
          g.RGBA8,
          w,
          h,
          0,
          g.RGBA,
          g.UNSIGNED_BYTE,
          null,
        );
        R.bindFramebuffer(g.DRAW_FRAMEBUFFER, preFb);
        R.framebufferTexture2D(
          g.DRAW_FRAMEBUFFER,
          g.COLOR_ATTACHMENT0,
          g.TEXTURE_2D,
          preTex,
          0,
        );
        preSize = [w, h];
        return true;
      } catch (e) {
        st.err = "pre: " + e;
        return false;
      } finally {
        R.bindFramebuffer(g.DRAW_FRAMEBUFFER, fbb);
        R.bindTexture(g.TEXTURE_2D, tb);
        R.activeTexture(at);
      }
    };
    const capturePre = () => {
      const w = game.width,
        h = game.height;
      if (!ensurePre(w, h)) {
        preState = 2;
        return;
      }
      const sc = R.isEnabled(g.SCISSOR_TEST),
        rd = curRead,
        dr = curDraw;
      if (sc) R.disable(g.SCISSOR_TEST);
      R.getError();
      R.bindFramebuffer(g.READ_FRAMEBUFFER, dr);
      R.bindFramebuffer(g.DRAW_FRAMEBUFFER, preFb);
      R.blitFramebuffer(0, 0, w, h, 0, 0, w, h, g.COLOR_BUFFER_BIT, g.NEAREST);
      const ok = R.getError() === 0;
      R.bindFramebuffer(g.READ_FRAMEBUFFER, rd);
      R.bindFramebuffer(g.DRAW_FRAMEBUFFER, dr);
      if (sc) R.enable(g.SCISSOR_TEST);
      if (ok) preState = 1;
      else {
        preState = 2;
        st.preFail = (st.preFail || 0) + 1;
      }
    };
    // ---------- environment table: sky colour by elevation, rebuilt from the visible sky every frame and smoothed over time ----------
    const ENV_N = 32;
    let envTex = [null, null],
      envFb = [null, null],
      envCur = 0,
      envT = 0,
      EP = null;
    const ENFS = `#version 300 es
precision highp float; precision highp int; precision highp sampler2D;
uniform sampler2D col, dep, envOld; uniform mat4 invVR; uniform float rate;
out vec4 o;
vec3 stepTo(vec3 a, vec3 b, float r){ vec3 d=b-a; vec3 s=d*r; vec3 m=sign(d)*min(abs(d),vec3(1.0/255.0)); return a+mix(m,s,step(abs(m),abs(s))); }   // moves at least one 8-bit step so slow smoothing never stalls
void main(){
  int bi=int(gl_FragCoord.x);
  vec3 acc=vec3(0.0); float n=0.0;
  for(int j=0;j<36;j++) for(int i=0;i<64;i++){
    vec2 q=(vec2(float(i),float(j))+0.5)/vec2(64.0,36.0);
    if(texture(dep,q).r<0.99999) continue;
    vec4 w=invVR*vec4(q*2.0-1.0,1.0,1.0);
    vec3 dir=normalize(w.xyz/w.w);
    int b=int(min(floor(sqrt(clamp(dir.y,0.0,1.0))*32.0),31.0));
    if(b!=bi) continue;
    acc+=textureLod(col,q,0.0).rgb; n+=1.0;
  }
  vec4 old=texelFetch(envOld,ivec2(bi,0),0);
  if(n>=1.0){ vec3 avg=acc/n; o= old.a>0.75 ? vec4(stepTo(old.rgb,avg,rate),1.0) : vec4(avg,1.0); return; }
  if(old.a>0.75){ o=old; return; }
  for(int k=1;k<32;k++){   // never seen this elevation yet: borrow from the nearest bin that was seen
    int a=bi-k, c2=bi+k;
    if(a>=0){ vec4 e=texelFetch(envOld,ivec2(a,0),0); if(e.a>0.75){ o=vec4(old.a<0.25 ? e.rgb : stepTo(old.rgb,e.rgb,rate),0.5); return; } }
    if(c2<32){ vec4 e=texelFetch(envOld,ivec2(c2,0),0); if(e.a>0.75){ o=vec4(old.a<0.25 ? e.rgb : stepTo(old.rgb,e.rgb,rate),0.5); return; } }
  }
  o=old;
}`;
    const ensureEnv = () => {
      // call inside withState
      if (envTex[0]) return;
      const init = new Uint8Array(ENV_N * 4);
      for (let i = 0; i < ENV_N; i++) {
        init[i * 4] = 34;
        init[i * 4 + 1] = 40;
        init[i * 4 + 2] = 46;
        init[i * 4 + 3] = 0;
      }
      R.activeTexture(g.TEXTURE0);
      R.bindSampler(0, null);
      for (let k = 0; k < 2; k++) {
        const t = R.createTexture();
        R.bindTexture(g.TEXTURE_2D, t);
        R.texParameteri(g.TEXTURE_2D, g.TEXTURE_MIN_FILTER, g.LINEAR);
        R.texParameteri(g.TEXTURE_2D, g.TEXTURE_MAG_FILTER, g.LINEAR);
        R.texParameteri(g.TEXTURE_2D, g.TEXTURE_WRAP_S, g.CLAMP_TO_EDGE);
        R.texParameteri(g.TEXTURE_2D, g.TEXTURE_WRAP_T, g.CLAMP_TO_EDGE);
        R.texImage2D(
          g.TEXTURE_2D,
          0,
          g.RGBA8,
          ENV_N,
          1,
          0,
          g.RGBA,
          g.UNSIGNED_BYTE,
          init,
        );
        const f = R.createFramebuffer();
        R.bindFramebuffer(g.FRAMEBUFFER, f);
        R.framebufferTexture2D(
          g.FRAMEBUFFER,
          g.COLOR_ATTACHMENT0,
          g.TEXTURE_2D,
          t,
          0,
        );
        envTex[k] = t;
        envFb[k] = f;
      }
    };
    let WP = null;
    // generic "run something with our own GL state, then put the game's state back exactly"
    const withState = (fn) => {
      const S = {
        read: curRead,
        draw: curDraw,
        prog: R.getParameter(g.CURRENT_PROGRAM),
        vao: R.getParameter(g.VERTEX_ARRAY_BINDING),
        at: R.getParameter(g.ACTIVE_TEXTURE),
        vp: R.getParameter(g.VIEWPORT),
        sc: R.isEnabled(g.SCISSOR_TEST),
        dt: R.isEnabled(g.DEPTH_TEST),
        bl: R.isEnabled(g.BLEND),
        cf: R.isEnabled(g.CULL_FACE),
        stn: R.isEnabled(g.STENCIL_TEST),
        cm: [
          !!(appliedBits & 1),
          !!(appliedBits & 2),
          !!(appliedBits & 4),
          !!(appliedBits & 8),
        ],
        bf: bs.f.slice(),
        be: bs.e.slice(),
      };
      const T = [];
      for (let u = 0; u < 5; u++) {
        R.activeTexture(g.TEXTURE0 + u);
        T.push([
          R.getParameter(g.TEXTURE_BINDING_2D),
          R.getParameter(g.SAMPLER_BINDING),
        ]);
      }
      try {
        fn();
      } finally {
        for (let u = 0; u < 5; u++) {
          R.activeTexture(g.TEXTURE0 + u);
          R.bindTexture(g.TEXTURE_2D, T[u][0]);
          R.bindSampler(u, T[u][1]);
        }
        R.activeTexture(S.at);
        R.bindVertexArray(S.vao);
        R.useProgram(S.prog);
        R.viewport(S.vp[0], S.vp[1], S.vp[2], S.vp[3]);
        (S.sc ? R.enable : R.disable)(g.SCISSOR_TEST);
        (S.dt ? R.enable : R.disable)(g.DEPTH_TEST);
        (S.bl ? R.enable : R.disable)(g.BLEND);
        (S.cf ? R.enable : R.disable)(g.CULL_FACE);
        (S.stn ? R.enable : R.disable)(g.STENCIL_TEST);
        R.colorMask(S.cm[0], S.cm[1], S.cm[2], S.cm[3]);
        R.blendFuncSeparate(S.bf[0], S.bf[1], S.bf[2], S.bf[3]);
        R.blendEquationSeparate(S.be[0], S.be[1]);
        R.bindFramebuffer(g.READ_FRAMEBUFFER, S.read);
        R.bindFramebuffer(g.DRAW_FRAMEBUFFER, S.draw);
      }
    };
    const runWet = (w, h, c, depth, usePre) => {
      if (!WP) {
        try {
          WP = mkP(FSQ, WFS);
          EP = mkP(FSQ, ENFS);
        } catch (e) {
          st.err = "wet: " + e;
          WET.enabled = false;
          return;
        }
      }
      const Vr = Float32Array.from(c.view);
      Vr[12] = Vr[13] = Vr[14] = 0;
      const VR = mul(c.proj, Vr),
        iVR = inv(VR),
        iv = inv(c.view);
      if (!iVR || !iv) return;
      const nowMs = performance.now();
      const doEnv = nowMs - envT >= 60; // the sky table only needs ~16 updates/s
      const rate =
        1 -
        Math.exp(
          -Math.min(0.5, (nowMs - envT) / 1000) / Math.max(0.02, WET.envSmooth),
        );
      if (doEnv) envT = nowMs;
      const u = WP.u;
      const sm = ground.sim();
      withState(() => {
        ensureEnv();
        R.bindFramebuffer(g.FRAMEBUFFER, null);
        R.activeTexture(g.TEXTURE0);
        R.bindSampler(0, null);
        R.bindTexture(g.TEXTURE_2D, colTex);
        R.copyTexSubImage2D(g.TEXTURE_2D, 0, 0, 0, 0, 0, w, h);
        R.activeTexture(g.TEXTURE1);
        R.bindTexture(g.TEXTURE_2D, depth);
        R.bindSampler(1, samp);
        R.activeTexture(g.TEXTURE2);
        R.bindSampler(2, null);
        R.bindTexture(g.TEXTURE_2D, usePre ? preTex : colTex);
        R.disable(g.SCISSOR_TEST);
        R.disable(g.DEPTH_TEST);
        R.disable(g.BLEND);
        R.disable(g.CULL_FACE);
        R.disable(g.STENCIL_TEST);
        R.colorMask(true, true, true, true);
        R.bindVertexArray(vao);
        // 1) update the environment table (sky colour by elevation) from the sky pixels currently visible
        if (doEnv) {
          const nxt = 1 - envCur;
          R.activeTexture(g.TEXTURE3);
          R.bindSampler(3, null);
          R.bindTexture(g.TEXTURE_2D, envTex[envCur]);
          R.bindFramebuffer(g.FRAMEBUFFER, envFb[nxt]);
          R.viewport(0, 0, ENV_N, 1);
          R.useProgram(EP.p);
          R.uniform1i(EP.u.col, 0);
          R.uniform1i(EP.u.dep, 1);
          R.uniform1i(EP.u.envOld, 3);
          R.uniformMatrix4fv(EP.u.invVR, false, new Float32Array(iVR));
          R.uniform1f(EP.u.rate, rate);
          R.drawArrays(g.TRIANGLES, 0, 3);
          envCur = nxt;
        }
        // 2) the wet-ground pass itself
        R.bindFramebuffer(g.FRAMEBUFFER, null);
        R.activeTexture(g.TEXTURE3);
        R.bindTexture(g.TEXTURE_2D, envTex[envCur]);
        R.viewport(0, 0, w, h);
        R.useProgram(WP.p);
        R.uniform1i(u.col, 0);
        R.uniform1i(u.dep, 1);
        R.uniform1i(u.pre, 2);
        R.uniform1i(u.env, 3);
        R.activeTexture(g.TEXTURE4);
        R.bindSampler(4, null);
        R.bindTexture(g.TEXTURE_2D, sm ? sm.tex : colTex);
        R.uniform1i(u.sim, 4);
        R.uniform1f(u.simOn, sm ? 1 : 0);
        R.uniform1f(u.simExt, sm ? sm.ext : 1);
        R.uniform1f(u.simN, sm ? sm.n : 1);
        R.uniform1f(u.splAmt, WET.splashAmt);
        R.uniform1f(u.foamAmt, WET.foam);
        R.uniformMatrix4fv(u.invVR, false, new Float32Array(iVR));
        R.uniformMatrix4fv(u.VR, false, new Float32Array(VR));
        R.uniform3f(u.camPos, iv[12], iv[13], iv[14]);
        R.uniform2f(u.res, w, h);
        R.uniform2f(u.AB, c.proj[10], c.proj[14]);
        R.uniform1f(u.tm, (performance.now() / 1000) % 3600);
        R.uniform1f(u.wet, WET.wet);
        R.uniform1f(u.puddle, WET.puddle);
        R.uniform1f(u.refl, WET.refl);
        R.uniform1f(u.ripple, WET.ripple);
        R.uniform1f(u.maxDist, WET.maxDist);
        R.uniform1f(u.cell, WET.cell);
        R.uniform1f(u.speed, WET.speed);
        R.uniform1f(u.maskOn, alphaFrame && WET.hideTanks ? 1 : 0);
        R.uniform1f(u.dbgMode, WET.debug | 0);
        R.drawArrays(g.TRIANGLES, 0, 3);
        st.wet = (st.wet || 0) + 1;
        if (usePre) st.pre = (st.pre || 0) + 1;
      });
    };
    // ---------- precipitation (rain / snow) + haze: world-anchored particles, hidden behind scenery via the depth texture ----------
    const FSQ = `#version 300 es
out vec2 uv; void main(){ vec2 p=vec2(float((gl_VertexID<<1)&2), float(gl_VertexID&2)); uv=p; gl_Position=vec4(p*2.0-1.0,0.0,1.0);}`;
    const HFS = `#version 300 es
precision highp float; precision highp int; precision highp sampler2D;
uniform sampler2D dep; uniform vec2 res, AB; uniform float mood; uniform vec3 hc; out vec4 o;
void main(){ float d=texture(dep,gl_FragCoord.xy/res).r; float dist=AB.y/(d*2.0-1.0+AB.x); float h=1.0-exp(-dist/12000.0); o=vec4(hc, mood*(0.12+0.7*h)); }`;
    const PVS = `#version 300 es
precision highp float; precision highp int;
uniform mat4 V, Pm; uniform vec3 Bsz, cmod, vel, fallv, wd; uniform vec2 res; uniform float tm, len, width, sizeW, sway, alpha; uniform int kind;
out vec2 qv; out float al; out float pd;
vec3 hash3(float n){ vec3 p=fract(vec3(n)*vec3(.1031,.1030,.0973)); p+=dot(p,p.yzx+33.33); return fract((p.xxy+p.yzz)*p.zyx); }
void main(){
  float id=float(gl_InstanceID);
  vec3 h=hash3(id+1.0); vec3 h2=hash3(id+7.31);
  float s=0.75+0.5*h2.x; float ls=mix(0.6,1.5,h2.y); float ws=mix(0.7,1.5,h2.z);
  vec3 pos=h*Bsz+fallv*s*tm+wd-cmod;
  pos=(fract(pos/Bsz)-0.5)*Bsz;
  if(kind==1){ pos.x+=sin(tm*1.1+h2.y*40.0)*sway; pos.z+=cos(tm*0.9+h2.z*40.0)*sway; }
  vec3 dir=normalize(vel);
  vec4 ca=Pm*(V*vec4(pos,1.0)); vec4 cb=Pm*(V*vec4(pos-dir*len*s*ls,1.0));
  float along=float(gl_VertexID>>1); float side=float(gl_VertexID&1)*2.0-1.0;
  if(ca.w<20.0||cb.w<20.0){ gl_Position=vec4(2.0,2.0,2.0,1.0); al=0.0; pd=0.0; qv=vec2(0.0); return; }
  float scale=Pm[1][1]*res.y*0.5;
  vec2 sa=ca.xy/ca.w*0.5*res, sb=cb.xy/cb.w*0.5*res; vec2 d=sb-sa; float l=length(d);
  vec2 dirS=l>0.001? d/l : vec2(1.0,0.0);
  float wpx= (kind==1 ? sizeW : width)*ws*scale/ca.w;
  float thin= kind==1 ? 1.0 : clamp(wpx,0.3,1.0);
  float hw=max(wpx,1.0)*0.5;
  vec2 perp=vec2(-dirS.y,dirS.x);
  vec2 px=mix(sa,sb,along)+dirS*hw*(along*2.0-1.0)+perp*side*hw;
  float z=mix(ca.z/ca.w,cb.z/cb.w,along);
  gl_Position=vec4(px/(0.5*res),z,1.0);
  float far=1.0-smoothstep(0.65,1.0,length(pos)/(0.5*min(Bsz.x,Bsz.z)));
  float near=smoothstep(40.0,260.0,ca.w);
  al=alpha*thin*far*near*(kind==1?1.0:mix(1.0,0.15,along));
  pd=mix(ca.w,cb.w,along); qv=vec2(side,along*2.0-1.0);
}`;
    const PFS = `#version 300 es
precision highp float; precision highp int; precision highp sampler2D;
uniform sampler2D dep; uniform vec2 res, AB; uniform vec3 col; uniform int kind;
in vec2 qv; in float al; in float pd; out vec4 o;
void main(){
  float sd=texture(dep,gl_FragCoord.xy/res).r;
  float sdist=AB.y/(sd*2.0-1.0+AB.x);
  float vis=clamp((sdist-pd)/30.0,0.0,1.0);
  float shape= kind==1 ? smoothstep(0.0,0.6,clamp(1.0-length(qv),0.0,1.0)) : (1.0-qv.x*qv.x);
  float a=al*vis*shape; if(a<0.004) discard; o=vec4(col,a); }`;
    const mkP = (vs, fs) => {
      const p = R.createProgram();
      for (const [t, s] of [
        [g.VERTEX_SHADER, vs],
        [g.FRAGMENT_SHADER, fs],
      ]) {
        const sh = R.createShader(t);
        R.shaderSource(sh, s);
        R.compileShader(sh);
        if (!R.getShaderParameter(sh, g.COMPILE_STATUS))
          throw new Error(R.getShaderInfoLog(sh));
        R.attachShader(p, sh);
      }
      R.linkProgram(p);
      if (!R.getProgramParameter(p, g.LINK_STATUS))
        throw new Error(R.getProgramInfoLog(p));
      const u = {};
      const n = R.getProgramParameter(p, g.ACTIVE_UNIFORMS);
      for (let i = 0; i < n; i++) {
        const nm = R.getActiveUniform(p, i).name;
        u[nm] = R.getUniformLocation(p, nm);
      }
      return {
        p,
        u,
      };
    };
    let PP = null,
      HP = null;
    const runPrecip = (w, h, c, depth) => {
      if (!PP) {
        try {
          PP = mkP(PVS, PFS);
          HP = mkP(FSQ, HFS);
        } catch (e) {
          st.err = String(e);
          PRECIP.type = "off";
          return;
        }
      }
      const iv = inv(c.view);
      if (!iv) return;
      const B = PRECIP.box;
      const cam = [iv[12], iv[13], iv[14]];
      const cmod = cam.map((x, i) => ((x % B[i]) + B[i]) % B[i]);
      const V = Float32Array.from(c.view);
      V[12] = V[13] = V[14] = 0;
      const snow = PRECIP.type === "snow";
      // wind with smooth gusts: speed = base*(1+gust*sin(om*t)); its integral keeps the drops continuous
      const T = performance.now() / 1000,
        om = 0.45,
        om2 = om * 0.8,
        gu = PRECIP.gust;
      const wx0 = snow ? PRECIP.windX * 0.2 : PRECIP.windX,
        wz0 = snow ? PRECIP.windZ * 0.2 : PRECIP.windZ;
      const vel = [
        wx0 * (1 + gu * Math.sin(T * om)),
        -(snow ? PRECIP.snowFall : PRECIP.fall),
        wz0 * (1 + gu * Math.cos(T * om2)),
      ];
      const fallv = [0, vel[1], 0];
      const wd = [
        wx0 * (T + (gu * (1 - Math.cos(T * om))) / om),
        0,
        wz0 * (T + (gu * Math.sin(T * om2)) / om2),
      ].map((x, i) => ((x % B[i]) + B[i]) % B[i]);
      const tm = T % 600;
      const count = Math.max(0, Math.min(PRECIP.count | 0, 40000));
      const S = {
        read: curRead,
        draw: curDraw,
        prog: R.getParameter(g.CURRENT_PROGRAM),
        vao: R.getParameter(g.VERTEX_ARRAY_BINDING),
        at: R.getParameter(g.ACTIVE_TEXTURE),
        vp: R.getParameter(g.VIEWPORT),
        sc: R.isEnabled(g.SCISSOR_TEST),
        dt: R.isEnabled(g.DEPTH_TEST),
        bl: R.isEnabled(g.BLEND),
        cf: R.isEnabled(g.CULL_FACE),
        stn: R.isEnabled(g.STENCIL_TEST),
        cm: [
          !!(appliedBits & 1),
          !!(appliedBits & 2),
          !!(appliedBits & 4),
          !!(appliedBits & 8),
        ],
        bf: bs.f.slice(),
        be: bs.e.slice(),
      };
      R.activeTexture(g.TEXTURE1);
      S.t1 = R.getParameter(g.TEXTURE_BINDING_2D);
      S.s1 = R.getParameter(g.SAMPLER_BINDING);
      try {
        R.bindFramebuffer(g.FRAMEBUFFER, null);
        R.bindTexture(g.TEXTURE_2D, depth);
        R.bindSampler(1, samp);
        R.disable(g.SCISSOR_TEST);
        R.disable(g.DEPTH_TEST);
        R.disable(g.CULL_FACE);
        R.disable(g.STENCIL_TEST);
        R.colorMask(true, true, true, true);
        R.enable(g.BLEND);
        R.blendFuncSeparate(g.SRC_ALPHA, g.ONE_MINUS_SRC_ALPHA, g.ZERO, g.ONE);
        R.blendEquationSeparate(g.FUNC_ADD, g.FUNC_ADD);
        R.viewport(0, 0, w, h);
        R.bindVertexArray(vao);
        if (PRECIP.mood > 0) {
          const hc = snow ? [0.78, 0.82, 0.88] : [0.5, 0.56, 0.64];
          R.useProgram(HP.p);
          R.uniform1i(HP.u.dep, 1);
          R.uniform2f(HP.u.res, w, h);
          R.uniform2f(HP.u.AB, c.proj[10], c.proj[14]);
          R.uniform1f(HP.u.mood, PRECIP.mood);
          R.uniform3f(HP.u.hc, hc[0], hc[1], hc[2]);
          R.drawArrays(g.TRIANGLES, 0, 3);
        }
        const u = PP.u;
        R.useProgram(PP.p);
        R.uniformMatrix4fv(u.V, false, V);
        R.uniformMatrix4fv(u.Pm, false, c.proj);
        R.uniform3f(u.Bsz, B[0], B[1], B[2]);
        R.uniform3f(u.cmod, cmod[0], cmod[1], cmod[2]);
        R.uniform3f(u.vel, vel[0], vel[1], vel[2]);
        R.uniform3f(u.fallv, fallv[0], fallv[1], fallv[2]);
        R.uniform3f(u.wd, wd[0], wd[1], wd[2]);
        R.uniform2f(u.res, w, h);
        R.uniform1f(u.tm, tm);
        R.uniform1f(u.len, snow ? 0 : PRECIP.len);
        R.uniform1f(u.width, PRECIP.width);
        R.uniform1f(u.sizeW, PRECIP.snowSize);
        R.uniform1f(u.sway, PRECIP.sway);
        R.uniform1f(u.alpha, PRECIP.alpha);
        R.uniform1i(u.kind, snow ? 1 : 0);
        R.uniform1i(u.dep, 1);
        R.uniform2f(u.AB, c.proj[10], c.proj[14]);
        const col = snow ? [1, 1, 1] : [0.8, 0.86, 0.95];
        R.uniform3f(u.col, col[0], col[1], col[2]);
        R.drawArraysInstanced(g.TRIANGLE_STRIP, 0, 4, count);
        st.precip = (st.precip || 0) + 1;
      } finally {
        R.bindTexture(g.TEXTURE_2D, S.t1);
        R.bindSampler(1, S.s1);
        R.activeTexture(S.at);
        R.bindVertexArray(S.vao);
        R.useProgram(S.prog);
        R.viewport(S.vp[0], S.vp[1], S.vp[2], S.vp[3]);
        (S.sc ? R.enable : R.disable)(g.SCISSOR_TEST);
        (S.dt ? R.enable : R.disable)(g.DEPTH_TEST);
        (S.bl ? R.enable : R.disable)(g.BLEND);
        (S.cf ? R.enable : R.disable)(g.CULL_FACE);
        (S.stn ? R.enable : R.disable)(g.STENCIL_TEST);
        R.colorMask(S.cm[0], S.cm[1], S.cm[2], S.cm[3]);
        R.blendFuncSeparate(S.bf[0], S.bf[1], S.bf[2], S.bf[3]);
        R.blendEquationSeparate(S.be[0], S.be[1]);
        R.bindFramebuffer(g.READ_FRAMEBUFFER, S.read);
        R.bindFramebuffer(g.DRAW_FRAMEBUFFER, S.draw);
      }
    };
    // ---------- depth of field: blur by distance from a focus distance (auto = the distance under a point on the screen, smoothed) ----------
    const FFS = `#version 300 es
precision highp float; precision highp int; precision highp sampler2D;
uniform sampler2D dep, old; uniform vec2 AB, fp; uniform float rate; out vec4 o;
float lin(float z){ return min(AB.y/(z*2.0-1.0+AB.x), 60000.0); }
vec2 enc(float x){ float v=floor(clamp(x/60000.0,0.0,1.0)*65535.0+0.5); float hi=floor(v/256.0); return vec2(hi, v-hi*256.0)/255.0; }
void main(){
  vec4 ov=texture(old,vec2(0.5))*255.0; float oldF=(floor(ov.r+0.5)*256.0+floor(ov.g+0.5))/65535.0*60000.0;
  float sum=0.0, n=0.0;
  for(int i=-1;i<=1;i++) for(int j=-1;j<=1;j++){ float z=texture(dep,fp+vec2(float(i),float(j))*0.01).r; if(z<0.99999){ sum+=lin(z); n+=1.0; } }
  float tgt = n>0.0 ? sum/n : oldF; float nf;
  if(oldF<1.0) nf=tgt; else { float diff=tgt-oldF; float stp=diff*rate; stp=sign(diff)*max(abs(stp), min(abs(diff),1.0)); nf=oldF+stp; }
  o=vec4(enc(nf),0.0,1.0);
}`;
    const DFS = `#version 300 es
precision highp float; precision highp int; precision highp sampler2D;
uniform sampler2D col, dep, foc; uniform vec2 res, AB, fpt; uniform float manF, useAuto, strength, maxR, range, scl; uniform int N, mode; out vec4 o;
float F;
float lin(float z){ return min(AB.y/(z*2.0-1.0+AB.x), 60000.0); }
float coc(float z){ float dist=lin(z); float r=abs(dist-F)/max(dist,1.0); return clamp((r-range)*strength*2.0,0.0,1.0)*maxR*scl; }
void main(){
  vec2 px=1.0/res; vec2 uv=gl_FragCoord.xy*px;
  vec4 f=texture(foc,vec2(0.5))*255.0; F = useAuto>0.5 ? (floor(f.r+0.5)*256.0+floor(f.g+0.5))/65535.0*60000.0 : manF; F=max(F,50.0);
  vec4 c0=texture(col,uv); float c=coc(texture(dep,uv).r); float cm=maxR*scl+1e-3;
  if(mode==1){ o=vec4(vec3(c/cm),1.0); return; }
  if(mode==2){ float inF=1.0-clamp(c/cm*4.0,0.0,1.0); vec3 t=mix(c0.rgb, vec3(0.1,1.0,0.3), 0.55*inF); if(length((uv-fpt)*vec2(res.x/res.y,1.0))<0.008 && useAuto>0.5) t=vec3(1.0,0.1,0.1); o=vec4(t,1.0); return; }
  if(c<0.6){ o=vec4(c0.rgb,1.0); return; }
  vec3 acc=c0.rgb; float ws=1.0;
  for(int i=0;i<48;i++){ if(i>=N) break;
    float fi=(float(i)+0.5)/float(N); float rad=sqrt(fi)*c; float a=float(i)*2.39996323;
    vec2 suv=uv+vec2(cos(a),sin(a))*rad*px; float cs=coc(texture(dep,suv).r);
    float w=clamp(cs-rad+1.0,0.0,1.0); acc+=texture(col,suv).rgb*w; ws+=w; }
  o=vec4(acc/ws,1.0);
}`;
    let DP = null,
      FP = null,
      focTex = [null, null],
      focFb = [null, null],
      focCur = 0,
      focT = 0;
    const ensureFoc = () => {
      // inside withState
      if (focTex[0]) return;
      R.activeTexture(g.TEXTURE0);
      R.bindSampler(0, null);
      for (let k = 0; k < 2; k++) {
        const t = R.createTexture();
        R.bindTexture(g.TEXTURE_2D, t);
        R.texParameteri(g.TEXTURE_2D, g.TEXTURE_MIN_FILTER, g.NEAREST);
        R.texParameteri(g.TEXTURE_2D, g.TEXTURE_MAG_FILTER, g.NEAREST);
        R.texParameteri(g.TEXTURE_2D, g.TEXTURE_WRAP_S, g.CLAMP_TO_EDGE);
        R.texParameteri(g.TEXTURE_2D, g.TEXTURE_WRAP_T, g.CLAMP_TO_EDGE);
        R.texImage2D(
          g.TEXTURE_2D,
          0,
          g.RGBA8,
          1,
          1,
          0,
          g.RGBA,
          g.UNSIGNED_BYTE,
          new Uint8Array(4),
        );
        const f = R.createFramebuffer();
        R.bindFramebuffer(g.FRAMEBUFFER, f);
        R.framebufferTexture2D(
          g.FRAMEBUFFER,
          g.COLOR_ATTACHMENT0,
          g.TEXTURE_2D,
          t,
          0,
        );
        focTex[k] = t;
        focFb[k] = f;
      }
    };
    const runDof = (w, h, c, depth) => {
      if (!DP) {
        try {
          DP = mkP(FSQ, DFS);
          FP = mkP(FSQ, FFS);
        } catch (e) {
          st.err = "dof: " + e;
          DOF.enabled = false;
          return;
        }
      }
      const D = DOF,
        nowMs = performance.now();
      const dt = Math.min(0.25, Math.max(0, (nowMs - focT) / 1000));
      focT = nowMs;
      const AB0 = c.proj[10],
        AB1 = c.proj[14];
      withState(() => {
        ensureFoc();
        R.bindFramebuffer(g.FRAMEBUFFER, null);
        R.activeTexture(g.TEXTURE1);
        R.bindTexture(g.TEXTURE_2D, depth);
        R.bindSampler(1, samp);
        R.disable(g.SCISSOR_TEST);
        R.disable(g.DEPTH_TEST);
        R.disable(g.BLEND);
        R.disable(g.CULL_FACE);
        R.disable(g.STENCIL_TEST);
        R.colorMask(true, true, true, true);
        R.bindVertexArray(vao);
        if (D.auto) {
          // 1) focus distance under the focus point, smoothed over time
          const nxt = 1 - focCur;
          R.activeTexture(g.TEXTURE3);
          R.bindSampler(3, null);
          R.bindTexture(g.TEXTURE_2D, focTex[focCur]);
          R.bindFramebuffer(g.FRAMEBUFFER, focFb[nxt]);
          R.viewport(0, 0, 1, 1);
          R.useProgram(FP.p);
          R.uniform1i(FP.u.dep, 1);
          R.uniform1i(FP.u.old, 3);
          R.uniform2f(FP.u.AB, AB0, AB1);
          R.uniform2f(FP.u.fp, 0.5, D.focusY);
          R.uniform1f(FP.u.rate, 1 - Math.exp(-dt * Math.max(0.1, D.speed)));
          R.drawArrays(g.TRIANGLES, 0, 3);
          focCur = nxt;
          R.bindFramebuffer(g.FRAMEBUFFER, null);
        }
        // 2) the blur itself
        R.activeTexture(g.TEXTURE0);
        R.bindSampler(0, null);
        R.bindTexture(g.TEXTURE_2D, colTex);
        R.copyTexSubImage2D(g.TEXTURE_2D, 0, 0, 0, 0, 0, w, h);
        R.activeTexture(g.TEXTURE3);
        R.bindSampler(3, null);
        R.bindTexture(g.TEXTURE_2D, focTex[focCur]);
        R.viewport(0, 0, w, h);
        R.useProgram(DP.p);
        const u = DP.u;
        R.uniform1i(u.col, 0);
        R.uniform1i(u.dep, 1);
        R.uniform1i(u.foc, 3);
        R.uniform2f(u.res, w, h);
        R.uniform2f(u.AB, AB0, AB1);
        R.uniform2f(u.fpt, 0.5, D.focusY);
        R.uniform1f(u.manF, D.focus);
        R.uniform1f(u.useAuto, D.auto ? 1 : 0);
        R.uniform1f(u.strength, D.strength);
        R.uniform1f(u.maxR, D.maxR);
        R.uniform1f(u.range, D.range);
        R.uniform1f(u.scl, h / 1080);
        R.uniform1i(u.N, Math.max(4, Math.min(48, D.samples | 0)));
        R.uniform1i(u.mode, D.mode | 0);
        R.drawArrays(g.TRIANGLES, 0, 3);
        st.dof = (st.dof || 0) + 1;
      });
    };
    // ---------- colour grading + vignette + sharpening: one last full-screen pass over the finished image ----------
    const GFS = `#version 300 es
precision highp float; precision highp sampler2D;
uniform sampler2D col; uniform vec2 res; uniform float gOn, sOn, expo, contrast, sat, temp, tint, vig, vigSize, amount, lim; out vec4 o;
void main(){
  vec2 px=1.0/res; vec2 uv=gl_FragCoord.xy*px; vec3 c=texture(col,uv).rgb;
  if(sOn>0.5){
    vec3 b=(texture(col,uv+px*vec2(1.0,0.0)).rgb+texture(col,uv-px*vec2(1.0,0.0)).rgb+texture(col,uv+px*vec2(0.0,1.0)).rgb+texture(col,uv-px*vec2(0.0,1.0)).rgb)*0.25;
    c+=clamp((c-b)*amount*2.0,-lim,lim);
  }
  if(gOn>0.5){
    c*=exp2(expo);
    c*=vec3(1.0+temp*0.18, 1.0-tint*0.12, 1.0-temp*0.18);
    c=(c-0.5)*contrast+0.5;
    float l=dot(c,vec3(0.2126,0.7152,0.0722)); c=mix(vec3(l),c,sat);
    float d=length((uv-0.5)*2.0)/1.41421; c*=1.0-vig*smoothstep(vigSize,1.0,d);
  }
  o=vec4(clamp(c,0.0,1.0),1.0);
}`;
    let GP = null;
    const runGrade = (w, h) => {
      if (!GP) {
        try {
          GP = mkP(FSQ, GFS);
        } catch (e) {
          st.err = "grade: " + e;
          GRADE.enabled = false;
          SHARP.enabled = false;
          return;
        }
      }
      withState(() => {
        R.bindFramebuffer(g.FRAMEBUFFER, null);
        R.activeTexture(g.TEXTURE0);
        R.bindSampler(0, null);
        R.bindTexture(g.TEXTURE_2D, colTex);
        R.copyTexSubImage2D(g.TEXTURE_2D, 0, 0, 0, 0, 0, w, h);
        R.disable(g.SCISSOR_TEST);
        R.disable(g.DEPTH_TEST);
        R.disable(g.BLEND);
        R.disable(g.CULL_FACE);
        R.disable(g.STENCIL_TEST);
        R.colorMask(true, true, true, true);
        R.viewport(0, 0, w, h);
        R.useProgram(GP.p);
        R.bindVertexArray(vao);
        const u = GP.u,
          Gr = GRADE,
          Sh = SHARP;
        R.uniform1i(u.col, 0);
        R.uniform2f(u.res, w, h);
        R.uniform1f(u.gOn, Gr.enabled ? 1 : 0);
        R.uniform1f(u.sOn, Sh.enabled ? 1 : 0);
        R.uniform1f(u.expo, Gr.expo);
        R.uniform1f(u.contrast, Gr.contrast);
        R.uniform1f(u.sat, Gr.sat);
        R.uniform1f(u.temp, Gr.temp);
        R.uniform1f(u.tint, Gr.tint);
        R.uniform1f(u.vig, Gr.vig);
        R.uniform1f(u.vigSize, Gr.vigSize);
        R.uniform1f(u.amount, Sh.amount);
        R.uniform1f(u.lim, Sh.limit);
        R.drawArrays(g.TRIANGLES, 0, 3);
        st.grade = (st.grade || 0) + 1;
      });
    };
    // ---------- atmosphere: fog (distance + height), a sun (glow, disc, god rays) and lightning (flash, bolt, thunder), in one pass ----------
    const AFS = `#version 300 es
precision highp float; precision highp int; precision highp sampler2D;
uniform sampler2D col, dep; uniform mat4 invVR, VR; uniform vec3 camPos, fogCol, sunDir, sunCol; uniform vec2 res;
uniform float fogOn, fogVis, fogStart, fogAmt, fogFall, fogBase, fogSky;
uniform float sunOn, sunSize, sunGlow, sunRays, sunInt;
uniform float lOn, flash, boltAmt, boltX, boltSeed;
out vec4 o;
float hash(float n){ return fract(sin(n*127.1)*43758.5453); }
float vn(float x){ float i=floor(x), f=fract(x); f=f*f*(3.0-2.0*f); return mix(hash(i),hash(i+1.0),f); }
float bolt(vec2 uv){
  float y=1.0-uv.y; float len=0.35+0.35*hash(boltSeed+3.0); if(y>len) return 0.0;
  float x0=boltX; float best=1e3;
  for(int b=0;b<3;b++){
    float fb=float(b); float yy=y-(b==0?0.0:0.10+0.08*fb*hash(boltSeed+fb));
    if(yy<0.0) continue;
    float dir=(b==1?-1.0:1.0)*(b==0?0.0:1.0);
    float x=x0+dir*yy*0.35+(vn(yy*9.0+boltSeed*7.0+fb*13.0)-0.5)*0.10*(0.4+yy*3.0);
    best=min(best, abs(uv.x-x)+ (b==0?0.0:0.004));
  }
  float fade=1.0-smoothstep(len*0.6,len,y);
  return (smoothstep(0.006,0.0,best)+0.35*smoothstep(0.03,0.0,best))*fade;
}
void main(){
  vec2 uv=gl_FragCoord.xy/res; vec3 c=texture(col,uv).rgb; float d=texture(dep,uv).r; bool sky=d>=0.99999;
  vec4 w=invVR*vec4(uv*2.0-1.0,d*2.0-1.0,1.0); vec3 P=w.xyz/w.w; vec3 V=normalize(P); float dist=sky?60000.0:length(P);
  float aspect=res.x/res.y;
  if(fogOn>0.5){
    float f;
    if(sky){ f=fogSky*fogAmt*(1.0-smoothstep(0.0,0.6,V.y)); }
    else {
      float t=max(dist-fogStart,0.0)/fogVis;
      float hf=exp(-fogFall*max(camPos.y+0.5*P.y-fogBase,0.0));
      f=(1.0-exp(-t*hf))*fogAmt;
    }
    vec3 fc=fogCol;
    if(sunOn>0.5){ float sc=pow(max(dot(V,sunDir),0.0),6.0); fc+=sunCol*sc*0.35*sunInt; }
    c=mix(c,fc,clamp(f,0.0,1.0));
  }
  if(sunOn>0.5){
    vec4 sc4=VR*vec4(sunDir,1.0);
    if(sc4.w>0.01){
      vec2 s=sc4.xy/sc4.w*0.5+0.5; vec2 dv=(uv-s)*vec2(aspect,1.0); float dd=length(dv);
      float skyW=sky?1.0:0.25;
      float disc=0.0;
      float glow=exp(-dd*dd/(sunSize*sunSize*40.0))*0.7+exp(-dd/(sunSize*6.0))*0.3;
      vec3 add=sunCol*(disc*3.0+glow*sunGlow*skyW)*sunInt;
      if(sunRays>0.01){
        float acc=0.0; for(int i=0;i<24;i++){ vec2 p=mix(uv,s,(float(i)+0.5)/24.0*0.9); vec4 q=texture(dep,p); acc+=(q.r>=0.99999 && p.x>0.0 && p.x<1.0 && p.y>0.0 && p.y<1.0)?1.0:0.0; }
        float edge=smoothstep(0.0,0.15,min(min(s.x,1.0-s.x),min(s.y,1.0-s.y))+0.15);
        add+=sunCol*(acc/24.0)*exp(-dd*1.6)*sunRays*0.55*sunInt*edge;
      }
      c+=add*smoothstep(0.01,0.2,sc4.w*0.2+0.01);
    }
  }
  if(lOn>0.5 && flash>0.001){
    vec3 fl=vec3(0.80,0.86,1.0);
    if(sky) c+=fl*flash*0.85; else c=c*(1.0+flash*0.55)+fl*flash*0.10;
    if(boltAmt>0.001 && sky){ float b=bolt(uv); c+=vec3(0.9,0.93,1.0)*b*boltAmt*1.4; }
  }
  o=vec4(c,1.0);
}`;
    let AP = null;
    const FOGC = [
      [0.62, 0.66, 0.7],
      [0.55, 0.65, 0.8],
      [0.86, 0.72, 0.58],
      [0.58, 0.7, 0.62],
    ];
    // lightning scheduler: a strike is 2-3 flashes close together; thunder (optional) arrives a moment later
    const LS = {
      next: performance.now() / 1000 + 6,
      t0: -99,
      off: [0, 0.12, 0.3],
      amp: [1, 0.6, 0.5],
      x: 0.5,
      seed: 1,
    };
    let ac = null,
      noiseBuf = null;
    const thunder = (delay) => {
      if (!LIGHT.thunder || LIGHT.vol <= 0) return;
      setTimeout(() => {
        try {
          ac = ac || new (window.AudioContext || window.webkitAudioContext)();
          if (ac.state === "suspended") ac.resume();
          if (!noiseBuf) {
            const n = ac.sampleRate * 4;
            noiseBuf = ac.createBuffer(1, n, ac.sampleRate);
            const d = noiseBuf.getChannelData(0);
            let b = 0;
            for (let i = 0; i < n; i++) {
              b = 0.97 * b + (Math.random() * 2 - 1) * 0.2;
              d[i] = b;
            }
          }
          const src = ac.createBufferSource();
          src.buffer = noiseBuf;
          const lp = ac.createBiquadFilter();
          lp.type = "lowpass";
          const gn = ac.createGain();
          const t = ac.currentTime;
          lp.frequency.setValueAtTime(420, t);
          lp.frequency.exponentialRampToValueAtTime(70, t + 3);
          gn.gain.setValueAtTime(0.0001, t);
          gn.gain.exponentialRampToValueAtTime(
            Math.max(0.001, LIGHT.vol * 1.6),
            t + 0.12,
          );
          gn.gain.exponentialRampToValueAtTime(0.0001, t + 3.6);
          src.connect(lp);
          lp.connect(gn);
          gn.connect(ac.destination);
          src.start(t);
          src.stop(t + 4);
        } catch (e) {}
      }, delay * 1000);
    };
    const strike = (now) => {
      LS.t0 = now;
      LS.x = 0.15 + Math.random() * 0.7;
      LS.seed = Math.random() * 50;
      LS.amp = [1, 0.5 + Math.random() * 0.4, 0.3 + Math.random() * 0.5];
      LS.off = [0, 0.09 + Math.random() * 0.08, 0.24 + Math.random() * 0.14];
      thunder(0.5 + Math.random() * 2.5);
    };
    const lightTick = () => {
      const now = performance.now() / 1000;
      const active =
        LIGHT.enabled && (!LIGHT.onlyRain || PRECIP.type === "rain");
      if (LIGHT.now) {
        LIGHT.now = 0;
        if (LIGHT.enabled) {
          strike(now);
        }
      } else if (active && now >= LS.next) {
        strike(now);
        LS.next = now + LIGHT.gap * (0.4 + Math.random() * 1.2);
      } else if (!active && now >= LS.next) LS.next = now + 2;
      if (!LIGHT.enabled)
        return {
          flash: 0,
          bolt: 0,
        };
      let f = 0,
        bl = 0;
      for (let i = 0; i < 3; i++) {
        const dt = now - LS.t0 - LS.off[i];
        if (dt >= 0) {
          const e = Math.exp(-dt / 0.07);
          f += LS.amp[i] * e;
          if (i === 0) bl = Math.exp(-dt / 0.12);
        }
      }
      return {
        flash: Math.min(1.5, f * LIGHT.flash),
        bolt: LIGHT.bolts ? bl : 0,
      };
    };
    const runAtm = (w, h, c, depth, lt) => {
      if (!AP) {
        try {
          AP = mkP(FSQ, AFS);
        } catch (e) {
          st.err = "atm: " + e;
          FOG.enabled = false;
          SUN.enabled = false;
          LIGHT.enabled = false;
          return;
        }
      }
      const Vr = Float32Array.from(c.view);
      Vr[12] = Vr[13] = Vr[14] = 0;
      const VR = mul(c.proj, Vr),
        iVR = inv(VR),
        iv = inv(c.view);
      if (!iVR || !iv) return;
      const sd = SUN.vec || [0, 1, 0],
        sunOn = !!(SUN.enabled && SUN.vec && (SUN.glow > 0 || SUN.rays > 0));
      const fcol = FOGC[FOG.color | 0] || FOGC[0],
        wm = Math.max(0, SUN.warmth),
        sc = [1, 0.97 - 0.37 * wm, 0.9 - 0.6 * wm];
      withState(() => {
        R.bindFramebuffer(g.FRAMEBUFFER, null);
        R.activeTexture(g.TEXTURE0);
        R.bindSampler(0, null);
        R.bindTexture(g.TEXTURE_2D, colTex);
        R.copyTexSubImage2D(g.TEXTURE_2D, 0, 0, 0, 0, 0, w, h);
        R.activeTexture(g.TEXTURE1);
        R.bindTexture(g.TEXTURE_2D, depth);
        R.bindSampler(1, samp);
        R.disable(g.SCISSOR_TEST);
        R.disable(g.DEPTH_TEST);
        R.disable(g.BLEND);
        R.disable(g.CULL_FACE);
        R.disable(g.STENCIL_TEST);
        R.colorMask(true, true, true, true);
        R.viewport(0, 0, w, h);
        R.useProgram(AP.p);
        R.bindVertexArray(vao);
        const u = AP.u;
        R.uniform1i(u.col, 0);
        R.uniform1i(u.dep, 1);
        R.uniform2f(u.res, w, h);
        R.uniformMatrix4fv(u.invVR, false, new Float32Array(iVR));
        R.uniformMatrix4fv(u.VR, false, new Float32Array(VR));
        R.uniform3f(u.camPos, iv[12], iv[13], iv[14]);
        R.uniform1f(u.fogOn, FOG.enabled ? 1 : 0);
        R.uniform1f(u.fogVis, Math.max(200, FOG.visibility));
        R.uniform1f(u.fogStart, FOG.start);
        R.uniform1f(u.fogAmt, FOG.amount);
        R.uniform1f(u.fogFall, FOG.fall * 0.002);
        R.uniform1f(u.fogBase, FOG.base);
        R.uniform1f(u.fogSky, FOG.sky);
        R.uniform3f(
          u.fogCol,
          fcol[0] * FOG.bright,
          fcol[1] * FOG.bright,
          fcol[2] * FOG.bright,
        );
        R.uniform1f(u.sunOn, sunOn ? 1 : 0);
        R.uniform3f(u.sunDir, sd[0], sd[1], sd[2]);
        R.uniform3f(u.sunCol, sc[0], sc[1], sc[2]);
        R.uniform1f(u.sunSize, 0.03);
        R.uniform1f(u.sunGlow, SUN.glow);
        R.uniform1f(u.sunRays, SUN.rays);
        R.uniform1f(u.sunInt, 1);
        R.uniform1f(u.lOn, LIGHT.enabled ? 1 : 0);
        R.uniform1f(u.flash, lt.flash);
        R.uniform1f(u.boltAmt, lt.bolt);
        R.uniform1f(u.boltX, LS.x);
        R.uniform1f(u.boltSeed, LS.seed);
        R.drawArrays(g.TRIANGLES, 0, 3);
        st.atm = (st.atm || 0) + 1;
      });
    };
    const ground = makeGround({
      R,
      g,
      st,
      WET,
      SNOW: SNOWG,
      PRECIP,
      withState,
      mkP,
      FSQ,
      mul,
      inv,
      ccv: () => ccv,
      masked: () => alphaFrame,
      col: () => colTex,
      vao: () => vao,
      samp: () => samp,
      pre: () => preTex,
    });
    // the game resolves its multisampled scene to the canvas with one colour blit: run our pass right after it
    hook(
      "blitFramebuffer",
      (prev) =>
        function (sx0, sy0, sx1, sy1, dx0, dy0, dx1, dy1, mask, filter) {
          const isResolve =
            curDraw === null &&
            curRead &&
            mask & g.COLOR_BUFFER_BIT &&
            sx1 - sx0 === game.width &&
            sy1 - sy0 === game.height;
          if (!isResolve) return prev.apply(this, arguments);
          const w = game.width,
            h = game.height;
          let cp = null,
            depth = null,
            blitDone = false;
          const frameMasked = alphaFrame;
          const usePre = preState === 1;
          preState = 0;
          const lt = lightTick();
          const wantAtm =
            FOG.enabled ||
            (SUN.enabled && SUN.vec && (SUN.glow > 0 || SUN.rays > 0)) ||
            lt.flash > 0.002;
          try {
            ground.tick(performance.now());
            const c = cap.get(curRead);
            const VP = c ? mul(c.proj, c.view) : null;
            const I = VP ? inv(VP) : null;
            if (!c) st.noCap++;
            const wantPr = PRECIP.type !== "off";
            const wantWet = WET.enabled && PRECIP.type === "rain",
              wantSnow = SNOWG.enabled && PRECIP.type === "snow";
            if (
              (P.enabled ||
                DOF.enabled ||
                GRADE.enabled ||
                SHARP.enabled ||
                wantAtm ||
                wantPr ||
                wantWet ||
                wantSnow) &&
              VP &&
              I
            ) {
              depth = findDepth();
              if (!depth) st.noDepth++;
              else if (P.enabled && prevVP) cp = mul(prevVP, I);
            }
            prevVP = VP;
            cap.clear();
            capP.clear();
            mainFb = curRead;
            const r = prev.apply(this, arguments);
            blitDone = true;
            if (depth && ((size[0] === w && size[1] === h) || build(w, h))) {
              if (wantWet && c) {
                ground.step(w, h, c, depth, false);
                runWet(w, h, c, depth, usePre);
                ground.runSpray(w, h, c, depth);
              }
              if (wantSnow && c) {
                ground.step(w, h, c, depth, true);
                ground.runSnow(w, h, c, depth, usePre);
              }
              if (wantAtm && c) runAtm(w, h, c, depth, lt);
              if (cp) runPass(w, h, cp, depth);
              if (DOF.enabled && c) runDof(w, h, c, depth);
              if (wantPr && c) runPrecip(w, h, c, depth);
              if (GRADE.enabled || SHARP.enabled) runGrade(w, h);
            }
            if (frameMasked) {
              fixAlpha();
              alphaFrame = false;
            }
            syncMask();
            return r;
          } catch (e) {
            st.err = String(e);
            cap.clear();
            capP.clear();
            if (!blitDone) {
              const r = prev.apply(this, arguments);
              if (frameMasked) {
                try {
                  fixAlpha();
                } catch (e2) {}
                alphaFrame = false;
              }
              return r;
            }
            try {
              if (frameMasked) fixAlpha();
            } catch (e2) {}
            alphaFrame = false;
            try {
              syncMask();
            } catch (e2) {}
          }
        },
    );

    return {
      st,
      destroy() {
        hooks.forEach((f) => f());
        try {
          ground.free();
        } catch (e) {}
        try {
          if (colTex) R.deleteTexture(colTex);
          if (prog) R.deleteProgram(prog);
          if (samp) R.deleteSampler(samp);
          if (preTex) R.deleteTexture(preTex);
          if (preFb) R.deleteFramebuffer(preFb);
          for (let k = 0; k < 2; k++) {
            if (focTex[k]) R.deleteTexture(focTex[k]);
            if (focFb[k]) R.deleteFramebuffer(focFb[k]);
          }
          for (let k = 0; k < 2; k++) {
            if (envTex[k]) R.deleteTexture(envTex[k]);
            if (envFb[k]) R.deleteFramebuffer(envFb[k]);
          }
        } catch (e) {}
      },
    };
  }




  // ======================= tank ground contact: snow tracks + puddle splashes =======================
  // The lower silhouette edge of every tank (the tank mask in the framebuffer alpha + the depth texture) is turned into points in world space:
  //  - snow: the points are stored in a persistent map that scrolls with the camera; the snow shader presses the snow away where tanks have been
  //  - rain: the points disturb a small wave simulation that the puddle shader reads (rings and foam behind tanks that drive through puddles)
  function makeGround(D) {
    const R = D.R,
      g = D.g,
      st = D.st,
      WET = D.WET,
      SNOW = D.SNOW,
      PRECIP = D.PRECIP;
    const TN = 2048,
      TS = 8; // snow track map: TN x TN cells of TS world units (scrolls with the camera)
    const TPER = 34,
      TCHEV = 0.55; // tread pattern: distance between cleats along the tread, and how far the cleats are bent back towards the middle (chevrons)
    const NL = 4,
      BH = 150; // ... with NL layers (must be a power of two, and the stamp shader writes exactly 4): ground heights are split into bands of BH units, band k lives in layer k mod NL.
    // Floors stacked above each other fall into different layers, so tracks on one level never overwrite tracks on another.
    const SN = 512,
      SS = 14; // splash wave grid: SN x SN cells of SS world units

    const DECFS = `#version 300 es
precision mediump float; uniform float k;
layout(location=0) out vec4 o0; layout(location=1) out vec4 o1; layout(location=2) out vec4 o2; layout(location=3) out vec4 o3;   // one per track layer
void main(){ o0=o1=o2=o3=vec4(k); }`;
    // wave simulation on a torus around the camera: r = height, g = previous height, b = foam, a = tank occupancy of the previous step.
    // A texel whose world cell changed because the window scrolled is reset; the edges of the window are damped.
    const SIMFS = `#version 300 es
precision highp float; precision highp int; precision highp sampler2D;
uniform sampler2D sim, occ; uniform ivec2 cCur, cPrev; uniform float c2, damp, fdec, ft;
out vec4 o;
const int N=${SN};
int wrapc(int i,int c){ return c+((i-c+N/2)&(N-1))-N/2; }
void main(){
  ivec2 i=ivec2(gl_FragCoord.xy);
  ivec2 kc=ivec2(wrapc(i.x,cCur.x),wrapc(i.y,cCur.y));
  ivec2 kp=ivec2(wrapc(i.x,cPrev.x),wrapc(i.y,cPrev.y));
  if(kc!=kp){ o=vec4(0.0); return; }
  ivec2 m=ivec2(N-1);
  vec4 s=texelFetch(sim,i,0);
  float sum=texelFetch(sim,(i+ivec2(1,0))&m,0).r+texelFetch(sim,(i+ivec2(-1,0))&m,0).r+texelFetch(sim,(i+ivec2(0,1))&m,0).r+texelFetch(sim,(i+ivec2(0,-1))&m,0).r;
  float hn=(2.0*s.r-s.g+c2*(sum-4.0*s.r))*damp;
  vec2 dd=abs(vec2(kc-cCur))/float(N/2);
  hn*=1.0-smoothstep(0.8,1.0,max(dd.x,dd.y));
  vec2 ocv=texelFetch(occ,i,0).rg; float oc=ocv.r, em=ocv.g;
  float rn=fract(sin(dot(vec2(i)+ft,vec2(12.9898,78.233)))*43758.5453);
  float imp=max(oc-s.a,0.0);   // only texels a tank has just moved into push the water; a parked tank is quiet
  hn+=imp+em*0.3*(rn-0.35);   // a moving tank also keeps churning the water along its sides
  float fnb=(texelFetch(sim,(i+ivec2(1,0))&m,0).b+texelFetch(sim,(i+ivec2(-1,0))&m,0).b+texelFetch(sim,(i+ivec2(0,1))&m,0).b+texelFetch(sim,(i+ivec2(0,-1))&m,0).b)*0.25;
  o=vec4(hn,s.r,max(max(mix(s.b,fnb,0.14)*fdec,imp),em*step(0.4,rn)*0.5),oc);   // foam drifts outwards a little, which spreads the spray sideways
}`;
    // snow cover on upward facing surfaces, with the track map pressed into it
    const SFS = `#version 300 es
precision highp float; precision highp int; precision highp sampler2D;
precision highp sampler2DArray;
uniform sampler2D col, dep, pre; uniform sampler2DArray trk; uniform mat4 invVR; uniform vec3 camPos; uniform vec2 res;
uniform float cov, range, tankOn, tracksOn, tDepth, bright, ext, dbgMode, hTol, bermK, patK; uniform vec2 lxz;
out vec4 o;
const float TE=1.0/${TN}.0;
const int TNI=${TN};
const int NL=${NL};              // track layers: the ground height (in bands of BH units) decides which layer a track lives in
const float BH=${BH}.0;
float hash(vec2 p){ vec3 p3=fract(vec3(p.xyx)*.1031); p3+=dot(p3,p3.yzx+33.33); return fract((p3.x+p3.y)*p3.z); }
float vn(vec2 p){ vec2 i=floor(p), f=fract(p); f=f*f*(3.0-2.0*f); return mix(mix(hash(i),hash(i+vec2(1.0,0.0)),f.x), mix(hash(i+vec2(0.0,1.0)),hash(i+vec2(1.0,1.0)),f.x), f.y); }
float fbm(vec2 p){ float a=0.5,s=0.0; for(int i=0;i<4;i++){ s+=a*vn(p); p=p*2.03+11.7; a*=0.5; } return s; }
vec3 posAt(vec2 uv){ float d=texture(dep,uv).r; vec4 w=invVR*vec4(uv*2.0-1.0,d*2.0-1.0,1.0); return w.xyz/w.w; }
// The track map is a top-down map, so it cannot tell floors on top of each other apart by itself. Every texel therefore also stores the height
// of the ground it was pressed into (g/b), and a texel only counts for a surface whose height matches. Bilinear filtering is done by hand
// for that reason: each of the four texels is checked against the surface height before they are blended.
float tk(ivec2 i, int l, float sy, float tol){
  vec4 t=texelFetch(trk,ivec3(i&ivec2(TNI-1),l),0);
  if(t.r<0.002) return 0.0;
  float h=(floor(t.g*255.0+0.5)*256.0+floor(t.b*255.0+0.5))/65535.0*40000.0-20000.0;
  return t.r*(1.0-smoothstep(tol,tol*1.8,abs(sy-h)));
}
// same as tk, plus the tread phase stored in alpha: (strength matching this height, phase)
vec2 tk2(ivec2 i, int l, float sy, float tol){
  vec4 t=texelFetch(trk,ivec3(i&ivec2(TNI-1),l),0);
  if(t.r<0.002) return vec2(0.0);
  float h=(floor(t.g*255.0+0.5)*256.0+floor(t.b*255.0+0.5))/65535.0*40000.0-20000.0;
  return vec2(t.r*(1.0-smoothstep(tol,tol*1.8,abs(sy-h))), floor(t.a*255.0+0.5)/256.0);
}
// (strength, tread phase) at a point. The phase is a repeating 0..1 value that is only stored per texel, so it is rebuilt smoothly: each texel's
// phase is taken relative to the strongest one (the shortest way round the 0/1 seam) and those offsets are blended.
vec2 trkP(vec2 tuv, float sy, float tol){
  float wd=tol*1.8; int b0=int(floor((sy-wd)/BH)), nb=int(floor((sy+wd)/BH))-b0;
  vec2 f=tuv*float(TNI)-0.5; ivec2 i0=ivec2(floor(f)); vec2 w=fract(f);
  vec4 bw=vec4((1.0-w.x)*(1.0-w.y), w.x*(1.0-w.y), (1.0-w.x)*w.y, w.x*w.y);
  float best=0.0; vec4 gv=vec4(0.0), pv=vec4(0.0);
  for(int l=0;l<NL;l++){
    if(((l-b0)&(NL-1))>nb) continue;
    vec2 a=tk2(i0,l,sy,tol), b=tk2(i0+ivec2(1,0),l,sy,tol), c=tk2(i0+ivec2(0,1),l,sy,tol), d=tk2(i0+ivec2(1,1),l,sy,tol);
    vec4 g4=vec4(a.x,b.x,c.x,d.x); float v=dot(g4,bw);
    if(v>best){ best=v; gv=g4; pv=vec4(a.y,b.y,c.y,d.y); }
  }
  float mx=max(max(gv.x,gv.y),max(gv.z,gv.w));
  float p0=gv.x>=mx?pv.x:(gv.y>=mx?pv.y:(gv.z>=mx?pv.z:pv.w));
  vec4 ok=step(0.02,gv)*bw; vec4 dd=fract(pv-p0+0.5)-0.5;
  return vec2(best, p0+dot(ok,dd)/max(dot(ok,vec4(1.0)),1e-4));
}
float trkS(vec2 tuv, float sy, float tol){
  float wd=tol*1.8; int b0=int(floor((sy-wd)/BH)), nb=int(floor((sy+wd)/BH))-b0;   // only the layers whose height bands reach this surface are read
  vec2 f=tuv*float(TNI)-0.5; ivec2 i0=ivec2(floor(f)); vec2 w=fract(f); float r=0.0;
  for(int l=0;l<NL;l++){
    if(((l-b0)&(NL-1))>nb) continue;
    r=max(r, mix(mix(tk(i0,l,sy,tol),tk(i0+ivec2(1,0),l,sy,tol),w.x), mix(tk(i0+ivec2(0,1),l,sy,tol),tk(i0+ivec2(1,1),l,sy,tol),w.x), w.y));
  }
  return r;
}
void main(){
  vec2 uv=gl_FragCoord.xy/res;
  vec4 b4=texture(col,uv); vec3 fin=b4.rgb; vec3 base=texture(pre,uv).rgb;
  float tank=tankOn>0.5 ? smoothstep(0.2,0.8,b4.a) : 0.0;
  if(dbgMode>0.5&&dbgMode<1.5){ o=vec4(vec3(tank),1.0); return; }
  float addl=max(max(fin.r-base.r,fin.g-base.g),fin.b-base.b);
  float cover=smoothstep(0.04,0.4,addl);   // pixels brightened by effects drawn after the opaque scene (flames, sparks...) stay on top
  float d0=texture(dep,uv).r;
  vec3 P=posAt(uv); float dist=length(P);
  if(d0>=0.99999||dist>range){ o=vec4(fin,1.0); return; }
  vec2 px=1.0/res;
  vec3 Pl=posAt(uv-vec2(px.x,0.0)), Pr=posAt(uv+vec2(px.x,0.0)), Pd=posAt(uv-vec2(0.0,px.y)), Pu=posAt(uv+vec2(0.0,px.y));
  vec3 dx= abs(length(Pr)-dist) < abs(dist-length(Pl)) ? Pr-P : P-Pl;
  vec3 dy= abs(length(Pu)-dist) < abs(dist-length(Pd)) ? Pu-P : P-Pd;
  vec3 N=normalize(cross(dx,dy)); if(dot(N,-P)<0.0) N=-N;
  vec2 wp=(P+camPos).xz;
  float fade=1.0-smoothstep(range*0.6,range,dist);
  float det=1.0-smoothstep(1500.0,7000.0,dist);        // fine detail is dropped with distance so it does not shimmer
  float up=smoothstep(0.5,0.92,N.y);
  float nz=fbm(wp*0.0035)*0.65+fbm(wp*0.017+31.0)*0.35;
  float grain=(vn(wp*0.35)-0.5)*0.18*det;
  float m=smoothstep(1.0-cov*1.4-0.1,1.0-cov*1.4+0.1,nz+grain+0.25*up);
  float mask=m*up*fade*(1.0-tank);
  float C=0.0, rim=0.0, shade=0.0, cleat=0.0;
  if(tracksOn>0.5 && (up*fade*(1.0-tank)>0.002 || dbgMode>1.5)){   // not only where the snow already covers: the banks make snow of their own
    vec2 tuv=fract(wp/ext);
    float win=1.0-smoothstep(0.8,0.95,max(abs(P.x),abs(P.z))/(ext*0.5));
    float sy=P.y+camPos.y, tol=hTol+dist*0.008;   // surface height; the window grows a little with distance because the depth gets coarser
    vec2 cp=trkP(tuv,sy,tol); float c0=cp.x;
    float q=0.5+0.5*cos(6.2831853*cp.y);   // cleat profile along the tread: q is high where a cleat bit into the snow
    cleat=smoothstep(0.55,0.95,q)*patK*(1.0-smoothstep(1500.0,5000.0,dist));   // fades out with distance so it does not shimmer
    float tpx=trkS(tuv+vec2(TE*4.0,0.0),sy,tol), tmx=trkS(tuv-vec2(TE*4.0,0.0),sy,tol), tpz=trkS(tuv+vec2(0.0,TE*4.0),sy,tol), tmz=trkS(tuv-vec2(0.0,TE*4.0),sy,tol);
    float cb=(tpx+tmx+tpz+tmz)*0.25;
    shade=clamp(dot(vec2(tpx-tmx,tpz-tmz),lxz)*0.6,-0.5,0.5)*win;   // slope of the groove walls and the pushed-up banks: lit on the side that faces the light
    // a track fills back in the way the snow first built up: patches of snow grow back from where the noise is highest
    float pa=clamp((nz-0.2)/0.6+grain*0.8,0.0,1.0);
    C=smoothstep(pa*0.8,pa*0.8+0.17,c0)*win;   // a fresh track (c0 = 1) is bare everywhere; snow only starts to return once it has aged a little
    rim=clamp(cb*2.4-c0*1.6,0.0,1.0)*win*(1.0-C);   // the bank of snow the tread pushed aside, along both edges of the track
  }
  if(dbgMode>1.5){ o=vec4(fin*0.35+vec3(C,rim*2.0,0.0)*0.65,1.0); return; }
  float l=dot(base,vec3(0.299,0.587,0.114));
  vec3 snowC=vec3(0.84,0.90,1.0)*clamp(0.40+1.55*l,0.0,1.1)*bright;
  // snow pushed out of the track piles up beside it: it is deeper there (brighter, and present even where the cover is patchy) and its slopes catch the light
  float bank=rim*bermK*(0.35+0.65*m)*clamp(cov*1.3,0.0,1.0);
  float mask2=max(mask,bank*up*fade*(1.0-tank));
  snowC*=1.0+0.22*bank+shade*0.7*min(bermK,1.5);
  // the tread never scrapes the ground clean: a thin, uneven layer of pressed snow stays, with the ground showing through in places
  float thin=clamp((1.0-tDepth)*(0.75+0.5*vn(wp*0.5)+0.25*vn(wp*0.09)),0.0,1.0);
  float cl=cleat*C;                                      // where a cleat dug in, the snow is scraped thinner and the ground shows; between the cleats it stays pressed flat
  thin*=1.0-0.8*cl; snowC*=1.0+0.12*C*patK*(1.0-cleat);
  float s=mask2*(1.0-C*(1.0-thin));
  vec3 c=mix(base,snowC,s);
  c*=1.0-0.3*cl*mask2;
  c=mix(c,c*vec3(0.86,0.90,0.96),C*mask2*0.4);          // what is left in the track is pressed, a little darker and bluer
  o=vec4(fin+(c-base)*(1.0-cover),1.0);
}`;

    let SM = null,
      DC = null,
      SW = null; // programs
    let trk = null,
      occ = null,
      sim = null,
      simCur = 0; // render targets
    let trkDead = false,
      simDead = false;
    let trkCell = null,
      simCell = null,
      simAcc = 0,
      simT = 0,
      decAcc = 0,
      decT = 0,
      lvT = performance.now();

    const rt = (ifmt, fmt, type, n) => {
      // call inside withState (leaves texture unit 0 and the framebuffer binding changed)
      const t = R.createTexture();
      R.activeTexture(g.TEXTURE0);
      R.bindTexture(g.TEXTURE_2D, t);
      R.texParameteri(g.TEXTURE_2D, g.TEXTURE_MIN_FILTER, g.LINEAR);
      R.texParameteri(g.TEXTURE_2D, g.TEXTURE_MAG_FILTER, g.LINEAR);
      R.texParameteri(g.TEXTURE_2D, g.TEXTURE_WRAP_S, g.REPEAT);
      R.texParameteri(g.TEXTURE_2D, g.TEXTURE_WRAP_T, g.REPEAT);
      R.texImage2D(g.TEXTURE_2D, 0, ifmt, n, n, 0, fmt, type, null);
      const f = R.createFramebuffer();
      R.bindFramebuffer(g.FRAMEBUFFER, f);
      R.framebufferTexture2D(
        g.FRAMEBUFFER,
        g.COLOR_ATTACHMENT0,
        g.TEXTURE_2D,
        t,
        0,
      );
      return {
        t,
        f,
        ok: R.checkFramebufferStatus(g.FRAMEBUFFER) === g.FRAMEBUFFER_COMPLETE,
      };
    };
    // the track map: an array texture with NL layers, and one framebuffer that has every layer attached (so one clear / one draw reaches all of them)
    const rtArr = (n, layers) => {
      const t = R.createTexture();
      R.activeTexture(g.TEXTURE0);
      R.bindTexture(g.TEXTURE_2D_ARRAY, t);
      R.texParameteri(g.TEXTURE_2D_ARRAY, g.TEXTURE_MIN_FILTER, g.NEAREST);
      R.texParameteri(g.TEXTURE_2D_ARRAY, g.TEXTURE_MAG_FILTER, g.NEAREST);
      R.texImage3D(
        g.TEXTURE_2D_ARRAY,
        0,
        g.RGBA8,
        n,
        n,
        layers,
        0,
        g.RGBA,
        g.UNSIGNED_BYTE,
        null,
      );
      const f = R.createFramebuffer();
      R.bindFramebuffer(g.FRAMEBUFFER, f);
      const bufs = [];
      for (let i = 0; i < layers; i++) {
        R.framebufferTextureLayer(
          g.FRAMEBUFFER,
          g.COLOR_ATTACHMENT0 + i,
          t,
          0,
          i,
        );
        bufs.push(g.COLOR_ATTACHMENT0 + i);
      }
      R.drawBuffers(bufs);
      let ok =
        R.checkFramebufferStatus(g.FRAMEBUFFER) === g.FRAMEBUFFER_COMPLETE;
      const fl = []; // and one framebuffer per layer, for drawing into a single layer
      for (let i = 0; i < layers; i++) {
        const fi = R.createFramebuffer();
        R.bindFramebuffer(g.FRAMEBUFFER, fi);
        R.framebufferTextureLayer(g.FRAMEBUFFER, g.COLOR_ATTACHMENT0, t, 0, i);
        ok =
          ok &&
          R.checkFramebufferStatus(g.FRAMEBUFFER) === g.FRAMEBUFFER_COMPLETE;
        fl.push(fi);
      }
      R.bindTexture(g.TEXTURE_2D_ARRAY, null);
      return {
        t,
        f,
        fl,
        ok,
      };
    };
    const killRT = (r) => {
      if (!r) return;
      R.deleteTexture(r.t);
      R.deleteFramebuffer(r.f);
      if (r.fl) r.fl.forEach((f) => R.deleteFramebuffer(f));
    };
    const mod = (a, n) => ((a % n) + n) % n;
    // clear what scrolled into a window-sized map: cells entered along x / z when the camera cell went from pc to cc
    const scrollClear = (fb, n, pc, cc) => {
      const dx = cc[0] - pc[0],
        dz = cc[1] - pc[1];
      if (!dx && !dz) return;
      const sb = R.getParameter(g.SCISSOR_BOX);
      R.bindFramebuffer(g.FRAMEBUFFER, fb);
      R.viewport(0, 0, n, n);
      R.clearColor(0, 0, 0, 0);
      R.enable(g.SCISSOR_TEST);
      const strips = (d, prev, cur, axis) => {
        if (!d) return;
        let s, len;
        if (Math.abs(d) >= n) {
          s = 0;
          len = n;
        } else if (d > 0) {
          s = mod(prev + n / 2, n);
          len = d;
        } else {
          s = mod(cur - n / 2, n);
          len = -d;
        }
        const l1 = Math.min(len, n - s);
        const rects = [[s, l1]];
        if (len > l1) rects.push([0, len - l1]);
        for (const [a, l] of rects) {
          if (axis === 0) R.scissor(a, 0, l, n);
          else R.scissor(0, a, n, l);
          R.clear(g.COLOR_BUFFER_BIT);
        }
      };
      strips(dx, pc[0], cc[0], 0);
      strips(dz, pc[1], cc[1], 1);
      R.disable(g.SCISSOR_TEST);
      R.scissor(sb[0], sb[1], sb[2], sb[3]);
      const cc2 = D.ccv();
      R.clearColor(cc2[0], cc2[1], cc2[2], cc2[3]);
    };
    const clearAll = (fb, n) => {
      R.bindFramebuffer(g.FRAMEBUFFER, fb);
      R.clearColor(0, 0, 0, 0);
      R.clear(g.COLOR_BUFFER_BIT);
      const cc = D.ccv();
      R.clearColor(cc[0], cc[1], cc[2], cc[3]);
    };
    // ---- tanks: the game draws each hull as one mesh whose tread vertices carry a trackSide attribute (-1 / +1), with a modelMatrix uniform.
    // Both are read here, so the treads (and the whole hull for puddles) are stamped exactly, whatever the camera sees.
    const TVS = `#version 300 es
precision highp float; precision highp int;
uniform mat4 M; uniform vec4 rect; uniform float ext, yoff;
out float hy; out vec2 wxz; out float lat; flat out vec2 hd;
void main(){
  vec2 c=vec2(float(gl_VertexID&1),float(gl_VertexID>>1));
  vec3 l=vec3(mix(rect.x,rect.y,c.x),yoff,mix(rect.z,rect.w,c.y));   // yoff = local height of the tread bottoms (0 when not stamping heights)
  vec3 lc=vec3((rect.x+rect.y)*0.5,yoff,(rect.z+rect.w)*0.5);
  vec3 w=(M*vec4(l,1.0)).xyz, wc=(M*vec4(lc,1.0)).xyz;
  hy=w.y;   // world height of the ground contact at this corner
  wxz=w.xz; lat=l.x-lc.x; hd=normalize((M*vec4(0.0,0.0,1.0,0.0)).xz);   // world position, sideways offset from the tread's middle, direction the hull points
  vec2 off=vec2(float(gl_InstanceID%3-1),float(gl_InstanceID/3-1));
  vec2 uv=fract(wc.xz/ext)+(w.xz-wc.xz)/ext+off;
  gl_Position=vec4(uv*2.0-1.0,0.0,1.0);
}`;
    const TFS = `#version 300 es
precision mediump float; uniform vec2 val; in float hy; out vec4 o; void main(){ o=vec4(val,0.0,0.0); }`;
    // snow track map: r = track, g/b = the height of the ground it was pressed into (16 bit, -20000..20000).
    // Only the layer of the current height band is written (the others keep what they have).
    // a = where along the tread this ground was pressed (cleat phase 0..1, from the world position along the hull direction, bent into chevrons by the sideways offset)
    const TFS2 = `#version 300 es
precision highp float; precision highp int;
uniform int lyr; in float hy; in vec2 wxz; in float lat; flat in vec2 hd; out vec4 o;
void main(){
  if((int(floor(hy/${BH}.0))&3)!=lyr) discard;
  float v=floor(clamp((hy+20000.0)/40000.0,0.0,1.0)*65535.0+0.5); float hi=floor(v/256.0);
  float ph=fract((dot(wxz,hd)+${TCHEV}*abs(lat))/${TPER}.0);
  o=vec4(1.0,hi/255.0,(v-hi*256.0)/255.0,min(floor(ph*256.0),255.0)/255.0);
}`;
    let TP = null,
      TP2 = null;
    const tankInfo = new WeakMap(),
      meshInfo = new WeakMap();
    let tanks = new Map(),
      prevTanks = new Map();
    const readAttr = (loc, first, count, comps) => {
      // read count vertices of a float attribute back from its buffer
      const buf = R.getVertexAttrib(loc, g.VERTEX_ATTRIB_ARRAY_BUFFER_BINDING);
      if (
        !buf ||
        R.getVertexAttrib(loc, g.VERTEX_ATTRIB_ARRAY_TYPE) !== g.FLOAT
      )
        return null;
      const stride =
          R.getVertexAttrib(loc, g.VERTEX_ATTRIB_ARRAY_STRIDE) || comps * 4,
        off = R.getVertexAttribOffset(loc, g.VERTEX_ATTRIB_ARRAY_POINTER);
      const prev = R.getParameter(g.ARRAY_BUFFER_BINDING);
      R.bindBuffer(g.ARRAY_BUFFER, buf);
      const size = R.getBufferParameter(g.ARRAY_BUFFER, g.BUFFER_SIZE),
        u8 = new Uint8Array(size);
      R.getBufferSubData(g.ARRAY_BUFFER, 0, u8);
      R.bindBuffer(g.ARRAY_BUFFER, prev);
      if (off + (first + count) * stride > size + stride) return null;
      const dv = new DataView(u8.buffer),
        out = new Float32Array(count * comps);
      for (let i = 0; i < count; i++)
        for (let k = 0; k < comps; k++)
          out[i * comps + k] = dv.getFloat32(
            off + (first + i) * stride + k * 4,
            true,
          );
      return out;
    };
    // called for each drawArrays of a tank program in the main pass
    const noteTank = (prog, first, count) => {
      let pi = tankInfo.get(prog);
      if (pi === undefined) {
        pi = {
          mm: R.getUniformLocation(prog, "modelMatrix"),
          pos: R.getAttribLocation(prog, "position"),
          ts: R.getAttribLocation(prog, "trackSide"),
        };
        if (!pi.mm || pi.pos < 0 || pi.ts < 0) pi = null;
        tankInfo.set(prog, pi);
      }
      if (
        !pi ||
        !R.getVertexAttrib(pi.ts, g.VERTEX_ATTRIB_ARRAY_ENABLED) ||
        !R.getVertexAttrib(pi.pos, g.VERTEX_ATTRIB_ARRAY_ENABLED)
      )
        return;
      const tb = R.getVertexAttrib(pi.ts, g.VERTEX_ATTRIB_ARRAY_BUFFER_BINDING);
      if (!tb) return;
      let mi = meshInfo.get(tb);
      if (!mi || mi.first !== first || mi.count !== count) {
        mi = {
          first,
          count,
          mesh: null,
        };
        meshInfo.set(tb, mi);
        try {
          const P = readAttr(pi.pos, first, count, 3),
            T = readAttr(pi.ts, first, count, 1);
          if (P && T) {
            const L = {
                x0: 1e9,
                x1: -1e9,
                z0: 1e9,
                z1: -1e9,
              },
              Rr = {
                x0: 1e9,
                x1: -1e9,
                z0: 1e9,
                z1: -1e9,
              },
              B = {
                x0: 1e9,
                x1: -1e9,
                z0: 1e9,
                z1: -1e9,
              };
            let nl = 0,
              nr = 0,
              gy = 1e9; // gy = lowest point of the treads in model space = where the tank touches the ground
            for (let i = 0; i < count; i++) {
              const x = P[i * 3],
                z = P[i * 3 + 2],
                t = T[i];
              B.x0 = Math.min(B.x0, x);
              B.x1 = Math.max(B.x1, x);
              B.z0 = Math.min(B.z0, z);
              B.z1 = Math.max(B.z1, z);
              const S = t > 0.5 ? Rr : t < -0.5 ? L : null;
              if (!S) continue;
              gy = Math.min(gy, P[i * 3 + 1]);
              if (S === Rr) nr++;
              else nl++;
              S.x0 = Math.min(S.x0, x);
              S.x1 = Math.max(S.x1, x);
              S.z0 = Math.min(S.z0, z);
              S.z1 = Math.max(S.z1, z);
            }
            if (nl > 2 && nr > 2)
              mi.mesh = {
                gy,
                tr: [
                  [L.x0, L.x1, L.z0, L.z1],
                  [Rr.x0, Rr.x1, Rr.z0, Rr.z1],
                ],
                side: [
                  [L.x0 - 60, L.x0, L.z0, L.z1],
                  [Rr.x1, Rr.x1 + 60, Rr.z0, Rr.z1],
                ],
                dims: [
                  L.x0,
                  Rr.x1,
                  Math.min(L.z0, Rr.z0),
                  Math.max(L.z1, Rr.z1),
                ],
                body: [[B.x0, B.x1, B.z0, B.z1]],
              };
          }
        } catch (e) {
          mi.mesh = null;
        }
      }
      if (!mi.mesh) return;
      const m = R.getUniform(prog, pi.mm);
      tanks.set(Math.round(m[12]) + "," + Math.round(m[14]), {
        m: Float32Array.from(m),
        mesh: mi.mesh,
      });
    };
    // draw the treads ('tr') or the whole hull ('body') of every tank seen last frame into a map (max blend)
    // hmode (snow): no blending, each stamped texel gets track = 1 plus the world height of the tread bottoms, so a track only exists at the height it was made
    const stampTanks = (fb, n, ts, cam, what, withSide, hmode) => {
      if (!prevTanks.size) return;
      if (hmode) {
        if (!TP2) TP2 = D.mkP(TVS, TFS2);
      } else if (!TP) TP = D.mkP(TVS, TFS);
      const Pg = hmode ? TP2 : TP,
        u = Pg.u,
        ext = n * ts;
      R.viewport(0, 0, n, n);
      if (hmode)
        R.disable(g.BLEND); // snow: plain overwrite, one pass per track layer (the shader drops the fragments that belong to another layer)
      else {
        R.enable(g.BLEND);
        R.blendEquation(g.MAX);
        R.blendFunc(g.ONE, g.ONE);
      }
      R.useProgram(Pg.p);
      R.uniform1f(u.ext, ext);
      const fbs = hmode ? fb.fl : [fb];
      for (let li = 0; li < fbs.length; li++) {
        R.bindFramebuffer(g.FRAMEBUFFER, fbs[li]);
        if (hmode) R.uniform1i(u.lyr, li);
        for (const t of prevTanks.values()) {
          if (
            Math.abs(t.m[12] - cam[0]) > ext * 0.45 ||
            Math.abs(t.m[14] - cam[2]) > ext * 0.45
          )
            continue; // outside the map window
          R.uniformMatrix4fv(u.M, false, t.m);
          R.uniform1f(u.yoff, hmode ? t.mesh.gy || 0 : 0);
          R.uniform2f(u.val, 1, 0);
          for (const r of t.mesh[what]) {
            R.uniform4f(u.rect, r[0], r[1], r[2], r[3]);
            R.drawArraysInstanced(g.TRIANGLE_STRIP, 0, 4, 9);
          }
          if (withSide && t.speed > 0.02) {
            // bands along the outer sides of the treads, strength by how fast the tank moves
            R.uniform2f(u.val, 0, t.speed);
            for (const r of t.mesh.side) {
              R.uniform4f(u.rect, r[0], r[1], r[2], r[3]);
              R.drawArraysInstanced(g.TRIANGLE_STRIP, 0, 4, 9);
            }
          }
        }
      }
      R.disable(g.BLEND);
    };

    // ---- spray: drops thrown up from both sides of a moving tank while it drives through a puddle. Fully procedural (no state): the position of each
    // drop is a function of the tank's matrix and velocity, the time and the drop number. They are drawn over the finished frame and hidden by the scene depth.
    const NP = 150;
    const PVS = `#version 300 es
precision highp float; precision highp int; precision highp sampler2D;
uniform mat4 VR, M; uniform vec3 camPos; uniform vec2 pj, vel, res; uniform float tm, sp, puddle, lat0, lat1, z0, z1, fwd;
out vec2 cr; out float al; out vec2 suv; out float ndcz; out float kind;
float hash(vec2 p){ vec3 p3=fract(vec3(p.xyx)*.1031); p3+=dot(p3,p3.yzx+33.33); return fract((p3.x+p3.y)*p3.z); }
float vn(vec2 p){ vec2 i=floor(p), f=fract(p); f=f*f*(3.0-2.0*f); return mix(mix(hash(i),hash(i+vec2(1.0,0.0)),f.x), mix(hash(i+vec2(0.0,1.0)),hash(i+vec2(1.0,1.0)),f.x), f.y); }
float fbm(vec2 p){ float a=0.5,s=0.0; for(int i=0;i<4;i++){ s+=a*vn(p); p=p*2.03+11.7; a*=0.5; } return s; }
void main(){
  gl_Position=vec4(2.0,2.0,2.0,1.0); al=0.0; cr=vec2(0.0); suv=vec2(0.0); ndcz=1.0; kind=0.0;
  int id=gl_InstanceID; float side=((id&1)==0)?-1.0:1.0; float k=float(id>>1);
  vec2 q=vec2(float(gl_VertexID&1),float(gl_VertexID>>1))*2.0-1.0; cr=q;
  float r1=hash(vec2(k,side*3.1)), r2=hash(vec2(k*1.7,side+9.0)), r3=hash(vec2(k+17.0,side*5.0)), r4=hash(vec2(k*0.3+5.0,side*2.0+1.0)), r5=hash(vec2(k*2.3+11.0,side)), r6=hash(vec2(k*3.7+2.0,side*8.0));
  if(r5>sp*1.15) return;
  bool mist=r6<0.12; kind=mist?1.0:0.0;
  float sv=0.4+0.6*sp;
  // drops are thrown mostly outwards and upwards, with a few high ones; each one lands when its flight time is over
  float up=mist?(60.0+120.0*r4):(220.0+520.0*r4*r4*sv+120.0*r2);
  float outS=(mist?0.35:1.0)*(80.0+300.0*r2*sv);
  float life=mist?(0.9+0.7*r2):max(0.25,2.0*up/980.0);
  float ph=fract(tm/life+r1); float age=ph*life;
  float zf=fwd>0.0?z1:z0, zb=fwd>0.0?z0:z1;
  float lz=mix(zf,zb,r3*r3*0.85);
  vec3 e=(M*vec4(side>0.0?lat1:lat0,0.0,lz,1.0)).xyz;
  vec3 od=normalize((M*vec4(side,0.0,0.0,0.0)).xyz);
  vec3 v3=vec3(vel.x,0.0,vel.y);
  vec3 pe=e-v3*age;
  float nn=fbm(pe.xz*0.0006)*0.7+fbm(pe.xz*0.0023+31.0)*0.3;
  float t0=mix(0.66,0.40,puddle);
  float pm=smoothstep(t0,t0+0.05,nn);
  if(pm<0.3) return;
  vec3 vv=od*outS+v3*0.55+vec3(0.0,up-980.0*age,0.0);
  vec3 pos=pe+od*outS*age+v3*age*0.55+vec3(0.0,up*age-490.0*age*age,0.0);
  if(pos.y<e.y) return;
  float rad=mist?(28.0+40.0*r3):(2.2+5.5*r3*r3);
  vec4 ch=VR*vec4(pos-camPos,1.0);
  if(ch.w<20.0) return;
  vec2 a=ch.xy/ch.w; vec2 ndc;
  if(mist){ ndc=a+q*rad*pj/ch.w; }
  else {
    vec4 ct=VR*vec4(pos-vv*0.045-camPos,1.0); if(ct.w<20.0) return;
    vec2 b=ct.xy/ct.w; float asp=res.x/res.y;
    vec2 d=(b-a)*vec2(asp,1.0); float len=length(d); vec2 dn=len>1e-6?d/len:vec2(0.0,1.0);
    vec2 pr=vec2(-dn.y,dn.x)/vec2(asp,1.0); float wN=rad*pj.y/ch.w;
    vec2 along=(b-a)*0.5+dn/vec2(asp,1.0)*wN;
    ndc=(a+b)*0.5+along*q.y+pr*wN*q.x;
  }
  gl_Position=vec4(ndc, ch.z/ch.w, 1.0);
  al=mist?(sin(ph*3.14159)*0.5*pm):((1.0-ph*ph)*pm); suv=a*0.5+0.5; ndcz=(ch.z/ch.w)*0.5+0.5;
}`;
    const PFS = `#version 300 es
precision highp float; precision highp sampler2D;
uniform sampler2D col, dep; uniform float amt;
in vec2 cr; in float al; in vec2 suv; in float ndcz; in float kind; out vec4 o;
void main(){
  if(al<=0.0) discard;
  float d=length(cr); if(d>1.0) discard;
  if(ndcz>texture(dep,suv).r+0.0004) discard;                 // behind the scene
  vec3 bg=texture(col,suv).rgb; float lum=dot(bg,vec3(0.299,0.587,0.114));
  float b=clamp(0.2+lum*1.5,0.2,0.95);                         // lit like what is behind it
  if(kind>0.5){ o=vec4(vec3(0.70,0.78,0.86)*b,al*0.22*(1.0-smoothstep(0.0,1.0,d))*amt); return; }   // fine mist
  // a water drop: mostly clear, it shows the scene slightly bent behind it, with a darker body and bright edge and glint
  vec3 bent=texture(col,suv+cr*vec2(0.004,0.004)).rgb;
  float rim=smoothstep(0.45,1.0,d);
  float z=sqrt(max(1.0-d*d,0.0));
  vec3 nrm=normalize(vec3(cr,z+0.2));
  float spec=pow(max(dot(nrm,normalize(vec3(-0.35,0.75,0.55))),0.0),18.0);
  vec3 c=bent*0.8+vec3(0.72,0.82,0.95)*b*(0.22*rim+0.9*spec);
  float a=al*amt*(0.4+0.45*rim+0.5*spec);
  o=vec4(c,clamp(a,0.0,1.0));
}`;
    let PP = null;
    const runSpray = (w, h, c, depth) => {
      if (!WET.splash || !prevTanks.size) return;
      let any = false;
      for (const t of prevTanks.values())
        if (t.speed > 0.05) {
          any = true;
          break;
        }
      if (!any) return;
      try {
        if (!PP) PP = D.mkP(PVS, PFS);
        const Vr = Float32Array.from(c.view);
        Vr[12] = Vr[13] = Vr[14] = 0;
        const iv = D.inv(c.view);
        if (!iv) return;
        const VR = D.mul(c.proj, Vr),
          cam = [iv[12], iv[13], iv[14]],
          u = PP.u,
          tm = (performance.now() / 1000) % 3600;
        D.withState(() => {
          R.bindFramebuffer(g.FRAMEBUFFER, null);
          R.activeTexture(g.TEXTURE0);
          R.bindSampler(0, null);
          R.bindTexture(g.TEXTURE_2D, D.col());
          R.copyTexSubImage2D(g.TEXTURE_2D, 0, 0, 0, 0, 0, w, h);
          R.activeTexture(g.TEXTURE1);
          R.bindTexture(g.TEXTURE_2D, depth);
          R.bindSampler(1, D.samp());
          R.disable(g.SCISSOR_TEST);
          R.disable(g.DEPTH_TEST);
          R.disable(g.CULL_FACE);
          R.disable(g.STENCIL_TEST);
          R.colorMask(true, true, true, true);
          R.viewport(0, 0, w, h);
          R.enable(g.BLEND);
          R.blendEquation(g.FUNC_ADD);
          R.blendFuncSeparate(
            g.SRC_ALPHA,
            g.ONE_MINUS_SRC_ALPHA,
            g.ZERO,
            g.ONE,
          );
          R.bindVertexArray(D.vao());
          R.useProgram(PP.p);
          R.uniform1i(u.col, 0);
          R.uniform1i(u.dep, 1);
          R.uniformMatrix4fv(u.VR, false, new Float32Array(VR));
          R.uniform3f(u.camPos, cam[0], cam[1], cam[2]);
          R.uniform2f(u.pj, c.proj[0], c.proj[5]);
          R.uniform2f(u.res, w, h);
          R.uniform1f(u.tm, tm);
          R.uniform1f(u.puddle, WET.puddle);
          R.uniform1f(u.amt, Math.min(1, 0.8 * WET.foam));
          for (const t of prevTanks.values()) {
            if (
              !(t.speed > 0.05) ||
              Math.hypot(t.m[12] - cam[0], t.m[14] - cam[2]) > 5000
            )
              continue;
            const d = t.mesh.dims;
            R.uniformMatrix4fv(u.M, false, t.m);
            R.uniform2f(u.vel, t.vx, t.vz);
            R.uniform1f(u.sp, t.speed);
            R.uniform1f(u.lat0, d[0]);
            R.uniform1f(u.lat1, d[1]);
            R.uniform1f(u.z0, d[2]);
            R.uniform1f(u.z1, d[3]);
            R.uniform1f(u.fwd, t.vx * t.m[8] + t.vz * t.m[10] >= 0 ? 1 : -1); // which end of the hull leads
            R.drawArraysInstanced(g.TRIANGLE_STRIP, 0, 4, NP * 2);
          }
        });
      } catch (e) {
        st.err = "spray: " + e;
        WET.splash = false;
      }
    };

    // snow build-up (0..1): rises while it snows, melts away otherwise
    const tick = (now) => {
      const dt = Math.min(0.25, Math.max(0, (now - lvT) / 1000));
      lvT = now;
      const oldTanks = prevTanks;
      prevTanks = tanks;
      tanks = new Map();
      for (const t of prevTanks.values()) {
        // velocity of each tank (smoothed) from how far it moved since the last frame
        let best = 1e9,
          bo = null;
        for (const o of oldTanks.values()) {
          const d = Math.hypot(t.m[12] - o.m[12], t.m[14] - o.m[14]);
          if (d < best) {
            best = d;
            bo = o;
          }
        }
        if (bo && best < 200) {
          const k = 0.35,
            vx = (t.m[12] - bo.m[12]) / Math.max(0.004, dt),
            vz = (t.m[14] - bo.m[14]) / Math.max(0.004, dt);
          t.vx = (bo.vx || 0) * (1 - k) + vx * k;
          t.vz = (bo.vz || 0) * (1 - k) + vz * k;
        } else {
          t.vx = t.vz = 0;
        }
        t.speed = Math.min(1, Math.hypot(t.vx, t.vz) / 400);
      }
      // snow build-up (0..1): rises while it snows, melts away otherwise
      if (PRECIP.type === "snow" && SNOW.enabled)
        SNOW.level =
          SNOW.build > 0.5 ? Math.min(1, SNOW.level + dt / SNOW.build) : 1;
      else SNOW.level = Math.max(0, SNOW.level - dt / Math.max(1, SNOW.melt));
    };

    // per frame, right after the game's frame has been resolved to the canvas: update the track map (snow) or the wave simulation (rain)
    const step = (w, h, c, depth, snow) => {
      if (snow ? !SNOW.tracks || trkDead : !WET.splash || simDead) return;
      try {
        if (!DC) {
          DC = D.mkP(D.FSQ, DECFS);
          SM = D.mkP(D.FSQ, SIMFS);
        }
        const Vr = Float32Array.from(c.view);
        Vr[12] = Vr[13] = Vr[14] = 0;
        const iVR = D.inv(D.mul(c.proj, Vr)),
          iv = D.inv(c.view);
        if (!iVR || !iv) return;
        const cam = [iv[12], iv[13], iv[14]],
          masked = D.masked(),
          now = performance.now();
        D.withState(() => {
          // render targets are created first: creating one leaves a texture bound on unit 0 and a framebuffer bound
          if (snow) {
            if (!trk) {
              trk = rtArr(TN, NL);
              trkCell = null;
              if (!trk.ok) {
                trkDead = true;
                return;
              }
              clearAll(trk.f, TN);
            }
          } else if (!sim) {
            if (
              !R.getExtension("EXT_color_buffer_float") &&
              !R.getExtension("EXT_color_buffer_half_float")
            ) {
              simDead = true;
              console.warn(
                "[postfx] puddle splashes need float render targets, which this browser does not offer",
              );
              return;
            }
            sim = [
              rt(g.RGBA16F, g.RGBA, g.HALF_FLOAT, SN),
              rt(g.RGBA16F, g.RGBA, g.HALF_FLOAT, SN),
            ];
            occ = rt(g.RG8, g.RG, g.UNSIGNED_BYTE, SN);
            if (!sim[0].ok || !sim[1].ok || !occ.ok) {
              simDead = true;
              st.err = "splash: render target incomplete";
              return;
            }
            clearAll(sim[0].f, SN);
            clearAll(sim[1].f, SN);
            clearAll(occ.f, SN);
            simCell = null;
            simT = now;
          }
          R.bindFramebuffer(g.FRAMEBUFFER, null);
          R.disable(g.SCISSOR_TEST);
          R.disable(g.DEPTH_TEST);
          R.disable(g.BLEND);
          R.disable(g.CULL_FACE);
          R.disable(g.STENCIL_TEST);
          R.colorMask(true, true, true, true);
          R.bindVertexArray(D.vao());
          if (snow) {
            const cx = Math.floor(cam[0] / TS),
              cz = Math.floor(cam[2] / TS);
            if (trkCell) scrollClear(trk.f, TN, trkCell, [cx, cz]);
            trkCell = [cx, cz];
            if (SNOW.clearReq) {
              SNOW.clearReq = 0;
              clearAll(trk.f, TN);
            }
            if (SNOW.fade > 0) {
              // tracks slowly fill in again: subtract one 8-bit step from the whole map every so often
              decAcc +=
                ((Math.min(0.25, Math.max(0, (now - decT) / 1000)) * 255) /
                  SNOW.fade) *
                (PRECIP.type === "snow" ? 1 : 0.25); // fresh snowfall fills tracks in; without it they only settle slowly
              const k = Math.min(255, Math.floor(decAcc));
              if (k >= 1) {
                decAcc -= k;
                R.bindFramebuffer(g.FRAMEBUFFER, trk.f);
                R.viewport(0, 0, TN, TN);
                R.enable(g.BLEND);
                R.blendEquation(g.FUNC_REVERSE_SUBTRACT);
                R.blendFunc(g.ONE, g.ONE);
                R.colorMask(true, false, false, false); // only the track strength fades, the stored height must stay
                R.useProgram(DC.p);
                R.uniform1f(DC.u.k, k / 255);
                R.drawArrays(g.TRIANGLES, 0, 3);
                R.disable(g.BLEND);
                R.colorMask(true, true, true, true);
              }
            }
            decT = now;
            stampTanks(trk, TN, TS, cam, "tr", false, true);
          } else {
            const cx = Math.floor(cam[0] / SS),
              cz = Math.floor(cam[2] / SS);
            if (!simCell) simCell = [cx, cz];
            clearAll(occ.f, SN);
            stampTanks(occ.f, SN, SS, cam, "tr", true);
            simAcc = Math.min(0.1, simAcc + Math.max(0, (now - simT) / 1000));
            simT = now;
            const steps = Math.min(3, Math.floor(simAcc * 60));
            simAcc -= steps / 60;
            if (steps > 0) {
              const u = SM.u;
              R.viewport(0, 0, SN, SN);
              R.useProgram(SM.p);
              R.uniform1i(u.sim, 2);
              R.uniform1i(u.occ, 3);
              R.uniform2i(u.cCur, cx, cz);
              R.uniform1f(
                u.c2,
                Math.min(0.45, Math.pow(WET.wake / (SS * 60), 2)),
              );
              R.uniform1f(
                u.damp,
                Math.exp(-1 / (60 * Math.max(0.2, WET.wakeLife))),
              );
              R.uniform1f(u.fdec, 0.955);
              for (let k = 0; k < steps; k++) {
                const nx = 1 - simCur;
                R.bindFramebuffer(g.FRAMEBUFFER, sim[nx].f);
                R.activeTexture(g.TEXTURE2);
                R.bindSampler(2, null);
                R.bindTexture(g.TEXTURE_2D, sim[simCur].t);
                R.activeTexture(g.TEXTURE3);
                R.bindSampler(3, null);
                R.bindTexture(g.TEXTURE_2D, occ.t);
                R.uniform2i(u.cPrev, simCell[0], simCell[1]);
                R.uniform1f(u.ft, (now * 0.001 + k * 0.37) % 97);
                R.drawArrays(g.TRIANGLES, 0, 3);
                simCur = nx;
                simCell = [cx, cz];
              }
            }
          }
        });
      } catch (e) {
        st.err = "ground: " + e;
        if (snow) trkDead = true;
        else simDead = true;
      }
    };

    // the snow shading pass over the finished frame
    const runSnow = (w, h, c, depth, usePre) => {
      if (SNOW.level <= 0.002) return;
      try {
        if (!SW) SW = D.mkP(D.FSQ, SFS);
        const Vr = Float32Array.from(c.view);
        Vr[12] = Vr[13] = Vr[14] = 0;
        const iVR = D.inv(D.mul(c.proj, Vr)),
          iv = D.inv(c.view);
        if (!iVR || !iv) return;
        const u = SW.u,
          tracks = !!(SNOW.tracks && trk && !trkDead);
        D.withState(() => {
          R.bindFramebuffer(g.FRAMEBUFFER, null);
          R.activeTexture(g.TEXTURE0);
          R.bindSampler(0, null);
          R.bindTexture(g.TEXTURE_2D, D.col());
          R.copyTexSubImage2D(g.TEXTURE_2D, 0, 0, 0, 0, 0, w, h);
          R.activeTexture(g.TEXTURE1);
          R.bindTexture(g.TEXTURE_2D, depth);
          R.bindSampler(1, D.samp());
          R.activeTexture(g.TEXTURE2);
          R.bindSampler(2, null);
          R.bindTexture(g.TEXTURE_2D, usePre && D.pre() ? D.pre() : D.col());
          R.activeTexture(g.TEXTURE3);
          R.bindSampler(3, null);
          if (tracks) R.bindTexture(g.TEXTURE_2D_ARRAY, trk.t);
          R.disable(g.SCISSOR_TEST);
          R.disable(g.DEPTH_TEST);
          R.disable(g.BLEND);
          R.disable(g.CULL_FACE);
          R.disable(g.STENCIL_TEST);
          R.colorMask(true, true, true, true);
          R.viewport(0, 0, w, h);
          R.useProgram(SW.p);
          R.bindVertexArray(D.vao());
          R.uniform1i(u.col, 0);
          R.uniform1i(u.dep, 1);
          R.uniform1i(u.pre, 2);
          R.uniform1i(u.trk, 3);
          R.uniformMatrix4fv(u.invVR, false, new Float32Array(iVR));
          R.uniform3f(u.camPos, iv[12], iv[13], iv[14]);
          R.uniform2f(u.res, w, h);
          R.uniform1f(u.cov, SNOW.cover * SNOW.level);
          R.uniform1f(u.range, SNOW.range);
          R.uniform1f(u.tankOn, D.masked() ? 1 : 0);
          R.uniform1f(u.tracksOn, tracks ? 1 : 0);
          R.uniform1f(u.tDepth, SNOW.trackDepth);
          R.uniform1f(u.bright, SNOW.bright);
          R.uniform1f(u.ext, TN * TS);
          R.uniform1f(u.dbgMode, SNOW.debug | 0);
          R.uniform1f(u.hTol, Math.max(1, SNOW.contact));
          R.uniform1f(u.bermK, SNOW.berm);
          R.uniform1f(u.patK, SNOW.pattern);
          {
            const lv = SUN.enabled && SUN.vec ? SUN.vec : [0.45, 0.8, 0.35],
              ll = Math.hypot(lv[0], lv[2]) || 1;
            R.uniform2f(u.lxz, lv[0] / ll, lv[2] / ll);
          } // horizontal direction towards the light
          R.drawArrays(g.TRIANGLES, 0, 3);
          st.snow = (st.snow || 0) + 1;
        });
      } catch (e) {
        st.err = "snow: " + e;
        SNOW.enabled = false;
      }
    };

    return {
      tick,
      step,
      runSnow,
      noteTank,
      runSpray,
      dbg: {
        stampTanks,
        prev: () => prevTanks,
      },
      sim: () =>
        sim && !simDead && WET.splash
          ? {
              tex: sim[simCur].t,
              ext: SN * SS,
              n: SN,
            }
          : null,
      free() {
        try {
          killRT(trk);
          killRT(occ);
          if (sim) {
            killRT(sim[0]);
            killRT(sim[1]);
          }
          for (const p of [TP, TP2, SM, DC, SW, PP])
            if (p) R.deleteProgram(p.p);
        } catch (e) {}
        trk = occ = sim = null;
      },
    };
  }




  // ======================= sun light: drives the game's own sun (direction, colour, ambient, shadows, sun sprite) =======================
  // It rewrites the values the game uploads (no reads back from the GPU), and does nothing at all while the sun is off.
  function installSun(game) {
    const g = game.getContext("webgl2");
    const proto = WebGL2RenderingContext.prototype;
    const R = new Proxy(
      {},
      {
        get:
          (_, n) =>
          (...a) =>
            proto[n].apply(g, a),
      },
    );
    const D2R = Math.PI / 180,
      P = SUN;
    const mul = (a, b) => {
      const r = new Float64Array(16);
      for (let c = 0; c < 4; c++)
        for (let rw = 0; rw < 4; rw++) {
          let s = 0;
          for (let k = 0; k < 4; k++) s += a[k * 4 + rw] * b[c * 4 + k];
          r[c * 4 + rw] = s;
        }
      return r;
    };
    const BIAS = [0.5, 0, 0, 0, 0, 0.5, 0, 0, 0, 0, 0.5, 0, 0.5, 0.5, 0.5, 1],
      BINV = [2, 0, 0, 0, 0, 2, 0, 0, 0, 0, 2, 0, -1, -1, -1, 1];
    const orthoInv = (p) => [
      1 / p[0],
      0,
      0,
      0,
      0,
      1 / p[5],
      0,
      0,
      0,
      0,
      1 / p[10],
      0,
      -p[12] / p[0],
      -p[13] / p[5],
      -p[14] / p[10],
      1,
    ];
    const same = (a, b) => {
      for (let k = 0; k < 16; k++)
        if (Math.abs(a[k] - b[k]) > 1e-6 * Math.max(1, Math.abs(a[k])))
          return false;
      return true;
    };
    const S = {
      dg: null,
      dn: null,
      ortho: [],
      cas: [],
      resolved: 0,
      budget: 0,
    };
    const norm3 = (v) => {
      const l = Math.hypot(v[0], v[1], v[2]) || 1;
      return [v[0] / l, v[1] / l, v[2] / l];
    };
    const active = () => Math.abs(P.yaw) > 0.01 || Math.abs(P.pitch) > 0.01;
    const newDir = () => {
      const d = S.dg;
      if (!d) return null;
      if (!active()) return d;
      const az = Math.atan2(d[0], d[2]) + P.yaw * D2R,
        el0 = Math.asin(Math.max(-1, Math.min(1, -d[1])));
      const el = Math.max(3 * D2R, Math.min(89 * D2R, el0 + P.pitch * D2R));
      return [
        Math.cos(el) * Math.sin(az),
        -Math.sin(el),
        Math.cos(el) * Math.cos(az),
      ];
    };
    // A sheared parallel projection can only reproduce a light direction that is not too far from the one the shadow maps were fitted for:
    // the more the light turns away from the original direction, the larger the shear and the further the receivers slide out of the
    // (fixed-size) shadow map, until the shadows vanish. So the direction used for SHADOWS is limited to a cone around the game's own
    // light direction (the lighting itself still uses the full requested direction).
    const MAX_CONE = 62 * D2R,
      MIN_NZ = Math.cos(MAX_CONE),
      MAX_SHEAR = Math.tan(MAX_CONE);
    const shadowDirOf = (dn) => {
      const dg = S.dg;
      if (!dg) return dn;
      const c = dn[0] * dg[0] + dn[1] * dg[1] + dn[2] * dg[2];
      if (c >= MIN_NZ) return dn;
      const px = dn[0] - c * dg[0],
        py = dn[1] - c * dg[1],
        pz = dn[2] - c * dg[2];
      const pl = Math.hypot(px, py, pz);
      if (pl < 1e-6) return dg;
      const s = Math.sin(MAX_CONE) / pl;
      return [
        dg[0] * MIN_NZ + px * s,
        dg[1] * MIN_NZ + py * s,
        dg[2] * MIN_NZ + pz * s,
      ];
    };
    const shearOf = (Rv, dnFull) => {
      // view-space shear that makes the (parallel) shadow projection run along dn
      const dn = shadowDirOf(dnFull);
      const nx = Rv[0] * dn[0] + Rv[1] * dn[1] + Rv[2] * dn[2],
        ny = Rv[3] * dn[0] + Rv[4] * dn[1] + Rv[5] * dn[2],
        nz = Rv[6] * dn[0] + Rv[7] * dn[1] + Rv[8] * dn[2];
      // FIX: the shadow camera's view-z axis (ez) points AGAINST the light (ez = -dg), so nz is NEGATIVE for every sensible sun direction.
      // The old code did Math.max(nz, 0.25), which turned every nz into +0.25: the shear was always the wrong sign and ~3.5x too big,
      // and shadows slid off the shadow map. A parallel projection only needs the line direction, so the sign of nz must be kept, and
      // |nz| bounded away from 0 (that is what the cone clamp in shadowDirOf guarantees).
      const z = nz < 0 ? Math.min(nz, -MIN_NZ) : Math.max(nz, MIN_NZ);
      return [
        Math.max(-MAX_SHEAR, Math.min(MAX_SHEAR, -nx / z)),
        Math.max(-MAX_SHEAR, Math.min(MAX_SHEAR, -ny / z)),
      ];
    };
    // shear about view depth zc (not about the shadow camera's origin): x' = x + a*(z - zc). Pivoting at the centre of the camera's depth
    // range keeps the area the cascade was fitted to inside the shadow map; shearing about z=0 slid it sideways by a*z and clipped the shadows.
    const SH = (a, b, zc) => [
      1, 0, 0, 0, 0, 1, 0, 0, a, b, 1, 0, -a * zc, -b * zc, 0, 1,
    ];
    // The map's own coordinates are "unit" coordinates u,v,w in [0,1]^3 (w = depth). Shearing the parallel projection along the new light
    // direction maps u' = u + A1*(w - wc), v' = v + A2*(w - wc). A shear alone makes the box that was fitted to the camera lean over, so
    // its footprint slides out of [0,1]^2 once the light has turned a bit and the shadows vanish. With fit = true the sheared box is
    // therefore also scaled/moved so that ALL of it lies inside the map again (costs resolution, never loses shadows). The result does
    // not depend on the pivot wc, so the pivot flickering between cascades/frames can no longer matter either.
    // Returns the column-major 4x4 A with (u',v',w') = A * (u,v,w,1).
    const unitMap = (Po, n0, n1, a, b, fit) => {
      const p10 = Po[10];
      const zc = pivotZ(Po);
      const wc = (zc * p10 + Po[14] + 1) / 2; // unit depth of the pivot
      const A1 = (n0 * a * 2) / p10,
        A2 = (n1 * b * 2) / p10;
      let sx = 1,
        sy = 1,
        tx = 0,
        ty = 0;
      if (fit) {
        const rng = (A) => {
          const lo = Math.min(-A * wc, A * (1 - wc)),
            hi = 1 + Math.max(-A * wc, A * (1 - wc));
          return [lo, hi];
        };
        const [ul, uh] = rng(A1),
          [vl, vh] = rng(A2);
        sx = 1 / (uh - ul);
        sy = 1 / (vh - vl);
        tx = -ul * sx;
        ty = -vl * sy;
      }
      return [
        sx, 0, 0, 0,
        0, sy, 0, 0,
        sx * A1, sy * A2, 1, 0,
        tx - sx * A1 * wc, ty - sy * A2 * wc, 0, 1,
      ];
    };
    const pivotOf = (Po) => (Math.abs(Po[10]) > 1e-9 ? -Po[14] / Po[10] : 0);
    // ---- which uniform is a location? The game keeps its own location objects, so each one is identified once by writing a marker through
    // our own location for every candidate name and reading it back through the game's (a few reads per location, then never again).
    const info = new WeakMap(),
      kinds = new WeakMap();
    let curProg = null,
      drawFb = null;
    const touched = new Set();
    const L = (p, n) => R.getUniformLocation(p, n);
    const getInfo = (p) => {
      let i = info.get(p);
      if (i) return i;
      i = {
        p,
        v3: {},
        m4: {},
        orig: {},
        rec: {},
        sig: "",
        msig: "",
        dsig: "",
        seeded: false,
        has3: false,
        hasM: false,
        depth: false,
        origP: null,
      };
      for (const n of [
        "sunLightDirection",
        "sunLightDirect",
        "sunLightAmbient",
      ]) {
        const l = L(p, n);
        if (l) i.v3[n] = l;
      }
      for (const n of [
        "sunShadowMatrix",
        "sunShadowNearMatrix",
        "sunTankMatrix",
        "projectionMatrix",
        "modelMatrix",
        "viewMatrix",
      ]) {
        const l = L(p, n);
        if (l) i.m4[n] = l;
      }
      i.has3 = Object.keys(i.v3).length > 0;
      const sun = !!L(p, "spriteSun"),
        shadowMats = !!(
          i.m4.sunShadowMatrix ||
          i.m4.sunShadowNearMatrix ||
          i.m4.sunTankMatrix
        ),
        lit = !!i.v3.sunLightDirection;
      const depth = !!(
        i.m4.projectionMatrix &&
        L(p, "modelViewMatrix") &&
        !i.m4.viewMatrix &&
        !lit &&
        !shadowMats
      );
      for (const n of Object.keys(i.m4)) {
        const wanted =
          n === "projectionMatrix"
            ? depth
            : n === "modelMatrix" || n === "viewMatrix"
              ? sun
              : shadowMats;
        if (!wanted) delete i.m4[n];
      }
      i.depth = depth;
      i.spr = sun;
      i.hasM = shadowMats;
      info.set(p, i);
      return i;
    };
    const resolve = (p, loc, size) => {
      const i = getInfo(p),
        pool = size === 3 ? i.v3 : i.m4,
        names = Object.keys(pool);
      if (!names.length) return 0;
      if (S.budget <= 0) return undefined;
      S.budget--;
      const mark =
        size === 3
          ? [7777.5, 8888.5, 9999.5]
          : Array.from(
              {
                length: 16,
              },
              (_, k) => 7777.5 + k,
            );
      for (const n of names) {
        const mine = pool[n];
        const saved = R.getUniform(p, mine);
        if (size === 3) R.uniform3fv(mine, new Float32Array(mark));
        else R.uniformMatrix4fv(mine, false, new Float32Array(mark));
        const probe = R.getUniform(p, loc);
        if (size === 3) R.uniform3fv(mine, saved);
        else R.uniformMatrix4fv(mine, false, saved);
        if (probe && probe[0] === 7777.5) {
          S.resolved++;
          return n;
        }
      }
      return 0;
    };
    const kindOf = (loc, size) => {
      let n = kinds.get(loc);
      if (n !== undefined) return n;
      if (!curProg) return undefined;
      n = resolve(curProg, loc, size);
      if (n !== undefined) kinds.set(loc, n);
      return n;
    };
    const sigOf = () =>
      P.yaw +
      "|" +
      P.pitch +
      "|" +
      P.intensity +
      "|" +
      P.ambient +
      "|" +
      P.warmth;
    let curSig = "";
    const modVec = (name, o) => {
      // -> the vec3 to upload for this uniform
      if (name === "sunLightDirection") {
        S.dg = norm3(o);
        const dn = newDir();
        S.dn = dn;
        P.vec = [-dn[0], -dn[1], -dn[2]];
        return active() ? dn : o;
      }
      if (name === "sunLightDirect") {
        const w = P.warmth;
        return P.intensity !== 1 || w !== 0
          ? [
              o[0] * P.intensity * (1 + 0.3 * w),
              o[1] * P.intensity,
              o[2] * P.intensity * (1 - 0.4 * w),
            ]
          : o;
      }
      return P.ambient !== 1
        ? [o[0] * P.ambient, o[1] * P.ambient, o[2] * P.ambient]
        : o;
    };
    // ---- DEBUG (console, prefix [sunDbg]): logs why a shadow cascade was not sheared. Remove once the bug is found.
    const dbgT = {};
    const dbg = (k, o) => {
      if (!window.__sunDbg) return; // opt-in: set window.__sunDbg = true in the console to see why a cascade was not sheared
      const t = performance.now();
      if (t - (dbgT[k] || 0) < 700) return;
      dbgT[k] = t;
      console.log("[sunDbg]", k, JSON.stringify(o));
    };
    // The shadow lookup matrices are NOT always BIAS * ortho * view: the tank cascade samples a sub-rectangle of an atlas, so its x/y scale
    // is a third of the depth camera's. So nothing is derived from the depth camera's x/y scale. The rows of the lookup matrix give the
    // shadow camera's axes directly (row0 ~ view x, row1 ~ view y, -row2 ~ view z), and only the depth scale (|row2|, which the lookup and
    // the depth pass share) is used to find the depth camera. A cascade is identified by that depth scale.
    const casKey = (k) => S.cas.find((c) => Math.abs(Math.log(c.k / k)) < 0.15);
    const pivotZ = (Po) =>
      // origin of the shadow camera when it lies inside the depth range (near/far cascades), otherwise the middle of the range (tank)
      Math.abs(Po[14]) <= 1 ? 0 : -Po[14] / Po[10];
    const dot3 = (u, v) => u[0] * v[0] + u[1] * v[1] + u[2] * v[2];
    const modShadow = (rec) => {
      // the world -> shadow-map matrix, with the shear applied to match the sheared depth camera
      const M = rec.orig;
      if (!M || !active() || !S.dn) return M;
      let why = "";
      for (;;) {
        const r0 = [M[0], M[4], M[8]],
          r1 = [M[1], M[5], M[9]],
          r2 = [M[2], M[6], M[10]];
        const n0 = Math.hypot(r0[0], r0[1], r0[2]),
          n1 = Math.hypot(r1[0], r1[1], r1[2]),
          n2 = Math.hypot(r2[0], r2[1], r2[2]);
        if (!(n0 > 1e-12 && n1 > 1e-12 && n2 > 1e-12)) {
          why = "degenerate";
          break;
        }
        const ex = [r0[0] / n0, r0[1] / n0, r0[2] / n0],
          ey = [r1[0] / n1, r1[1] / n1, r1[2] / n1],
          ez = [-r2[0] / n2, -r2[1] / n2, -r2[2] / n2];
        const cr = [
          ex[1] * ey[2] - ex[2] * ey[1],
          ex[2] * ey[0] - ex[0] * ey[2],
          ex[0] * ey[1] - ex[1] * ey[0],
        ];
        if (
          Math.abs(dot3(ex, ey)) > 0.05 ||
          Math.abs(dot3(ex, ez)) > 0.05 ||
          Math.abs(dot3(ey, ez)) > 0.05 ||
          dot3(cr, ez) < 0.9
        ) {
          why = "notOrthonormal";
          break;
        }
        if (S.dg && Math.abs(dot3(ez, S.dg)) < 0.85) {
          why = "axis dot=" + dot3(ez, S.dg).toFixed(3);
          break;
        }
        const k = 2 * n2; // = |p10| of the depth camera
        const Po = S.ortho.find((q) => Math.abs(Math.abs(q[10]) / k - 1) < 0.03);
        if (!Po) {
          why = "noDepthCam k=" + k.toExponential(3);
          break;
        }
        let e = casKey(k);
        if (!e) {
          e = {
            k,
          };
          S.cas.push(e);
        }
        e.k = k;
        e.Rv = [ex[0], ex[1], ex[2], ey[0], ey[1], ey[2], ez[0], ez[1], ez[2]];
        rec.k = k;
        const [a, b] = shearOf(e.Rv, S.dn);
        // The tank cascade samples a sub-rectangle of an atlas, so its (shear-only) correction needs the atlas scale n0/n1; the near/far
        // cascades own the whole map and additionally get re-fitted (see unitMap).
        e.fit = rec.n !== "sunTankMatrix";
        const A = e.fit
          ? unitMap(Po, 0.5 * Math.abs(Po[0]), 0.5 * Math.abs(Po[5]), a, b, true)
          : unitMap(Po, n0, n1, a, b, false);
        // world -> sheared & re-fitted shadow map: M' = A * M
        const N = Float32Array.from(mul(A, M));
        dbg("lookupOK " + rec.n, {
          k: +k.toPrecision(4),
          a: +a.toFixed(3),
          b: +b.toFixed(3),
          fit: e.fit,
          sx: +A[0].toFixed(3),
          sy: +A[5].toFixed(3),
        });
        return N;
      }
      dbg("lookupUnsheared " + rec.n, {
        why,
        yaw: P.yaw,
        pitch: P.pitch,
      });
      // not sheared here, so make sure the depth pass for this cascade is not sheared either (both passes must agree)
      if (rec.k) {
        for (let q = S.cas.length - 1; q >= 0; q--)
          if (Math.abs(Math.log(S.cas[q].k / rec.k)) < 0.15) S.cas.splice(q, 1);
        rec.k = null;
      }
      return M;
    };
    const modDepthProj = (Pm) => {
      if (
        Math.abs(Pm[11]) > 1e-6 ||
        Math.abs(Pm[15] - 1) > 1e-6 ||
        Math.abs(Pm[0]) < 1e-9
      )
        return Pm; // only the orthographic (shadow) cameras
      if (!S.ortho.some((q) => same(q, Pm))) {
        // FIX: the list is persistent (no longer emptied every frame). The game only uploads a depth camera's projection when it changes,
        // so a cascade whose camera did not change this frame (typically the tank cascade) used to have no entry at lookup time and was left
        // unsheared, flipping the shadows between the sun's direction and the game's every few frames. A new upload replaces the entry
        // of the same cascade (same depth scale) instead of piling up.
        const kq = Math.abs(Pm[10]);
        const at = S.ortho.findIndex(
          (q) => Math.abs(Math.log(Math.abs(q[10]) / kq)) < 0.15,
        );
        if (at >= 0) S.ortho[at] = Float64Array.from(Pm);
        else {
          S.ortho.push(Float64Array.from(Pm));
          if (S.ortho.length > 8) S.ortho.shift();
        }
      }
      if (!active() || !S.dn) return Pm;
      const c = casKey(Math.abs(Pm[10]));
      if (!c) {
        dbg("depthNoCascade", {
          k: +Math.abs(Pm[10]).toPrecision(4),
          known: S.cas.map((q) => +q.k.toPrecision(4)),
        });
        return Pm;
      }
      const [a, b] = shearOf(c.Rv, S.dn);
      // clip' = BINV * A * BIAS * clip (the same map the lookup matrix gets, written in clip space); in the depth pass the camera is
      // always the cascade's own full map, so its local scale 0.5*|p0|, 0.5*|p5| is used for the shear
      const A = unitMap(
        Pm,
        0.5 * Math.abs(Pm[0]),
        0.5 * Math.abs(Pm[5]),
        a,
        b,
        !!c.fit,
      );
      return mul(mul(BINV, mul(A, BIAS)), Pm);
    };
    const modSprite = (i, M) => {
      // the sun billboard: keep it on the light's direction
      const V = i.view;
      if (!V || !S.dg || !S.dn || !active()) return M;
      const t = [V[12], V[13], V[14]],
        C = [
          -(V[0] * t[0] + V[1] * t[1] + V[2] * t[2]),
          -(V[4] * t[0] + V[5] * t[1] + V[6] * t[2]),
          -(V[8] * t[0] + V[9] * t[1] + V[10] * t[2]),
        ];
      const v = [M[12] - C[0], M[13] - C[1], M[14] - C[2]],
        len = Math.hypot(v[0], v[1], v[2]);
      if (len < 1500) return M;
      if (-(v[0] * S.dg[0] + v[1] * S.dg[1] + v[2] * S.dg[2]) / len < 0.995)
        return M;
      const N = Float32Array.from(M);
      N[12] = C[0] - S.dn[0] * len;
      N[13] = C[1] - S.dn[1] * len;
      N[14] = C[2] - S.dn[2] * len;
      return N;
    };
    // ---- wrap the uniform setters; everything is a pass-through unless the sun is on
    const hooks = [];
    let installed = false;
    const hook = (n, fn) => {
      const own = Object.prototype.hasOwnProperty.call(g, n);
      const prev = g[n];
      g[n] = fn(prev);
      hooks.push(() => {
        if (own) g[n] = prev;
        else delete g[n];
      });
    };
    const writeMats = (i, restoreOnly) => {
      for (const n in i.rec) {
        const r = i.rec[n];
        r.n = n;
        if (!r.orig) continue;
        R.uniformMatrix4fv(
          i.m4[n],
          false,
          Float32Array.from(restoreOnly ? r.orig : modShadow(r)),
        );
      }
    };
    const install = () => {
      if (installed) return;
      installed = true;
      curSig = sigOf();
      hook(
        "useProgram",
        (prev) =>
          function (p) {
            curProg = p;
            return prev.apply(this, arguments);
          },
      );
      hook(
        "bindFramebuffer",
        (prev) =>
          function (t, fb) {
            if (t === g.FRAMEBUFFER || t === g.DRAW_FRAMEBUFFER) drawFb = fb;
            return prev.apply(this, arguments);
          },
      );
      hook(
        "blitFramebuffer",
        (prev) =>
          function () {
            const r = prev.apply(this, arguments);
            if (drawFb === null) {
              S.frame = (S.frame || 0) + 1;
              S.budget = 6;
              curSig = sigOf();
            }
            return r;
          },
      );
      const vec3 = (prev, self, loc, o, n, is3f) => {
        const i = getInfo(curProg);
        i.orig[n] = o;
        i.sig = curSig;
        touched.add(curProg);
        const m = modVec(n, o);
        return is3f
          ? prev.call(self, loc, m[0], m[1], m[2])
          : prev.call(self, loc, Float32Array.from(m));
      };
      hook(
        "uniform3fv",
        (prev) =>
          function (loc, v) {
            const n = curProg && kindOf(loc, 3);
            return n
              ? vec3(prev, this, loc, [v[0], v[1], v[2]], n, false)
              : prev.apply(this, arguments);
          },
      );
      hook(
        "uniform3f",
        (prev) =>
          function (loc, x, y, z) {
            const n = curProg && kindOf(loc, 3);
            return n
              ? vec3(prev, this, loc, [x, y, z], n, true)
              : prev.apply(this, arguments);
          },
      );
      hook(
        "uniformMatrix4fv",
        (prev) =>
          function (loc, tr, v) {
            const n = curProg && kindOf(loc, 16);
            if (!n || tr) return prev.apply(this, arguments);
            const i = getInfo(curProg),
              M = Float32Array.from(v);
            let out = null;
            if (n === "viewMatrix") {
              i.view = M;
              // world-space camera position (view = world -> camera)
              S.cam = [
                -(M[0] * M[12] + M[1] * M[13] + M[2] * M[14]),
                -(M[4] * M[12] + M[5] * M[13] + M[6] * M[14]),
                -(M[8] * M[12] + M[9] * M[13] + M[10] * M[14]),
              ];
            }
            else if (n === "projectionMatrix") {
              i.origP = M;
              out = modDepthProj(M);
              touched.add(curProg);
            } else if (n === "modelMatrix") out = modSprite(i, M);
            else {
              const r =
                i.rec[n] ||
                (i.rec[n] = {
                  orig: null,
                  po: null,
                });
              r.orig = M;
              r.n = n;
              i.msig = curSig;
              touched.add(curProg);
              out = modShadow(r);
            }
            return out
              ? prev.call(this, loc, false, Float32Array.from(out))
              : prev.apply(this, arguments);
          },
      );
      // the game uploads many of these only when they change, so also push our values whenever the settings change (writes only)
      [
        "drawElements",
        "drawArrays",
        "drawElementsInstanced",
        "drawArraysInstanced",
        "drawRangeElements",
      ].forEach((n) =>
        hook(
          n,
          (prev) =>
            function () {
              if (curProg) {
                const i = getInfo(curProg);
                // FIX ("ball" at high Rotate): the game draws its round sun sprite into the shadow maps (a depth-only pass: colour mask all
                // false), so the sprite casts a shadow. With the game's own light that shadow lands far outside the visible area; once the
                // light is turned it falls on the ground in front of the camera as a dark ball that follows the camera (the cascades follow
                // it). The sprite is not a real object, so its depth-only draw is skipped while the sun is modified.
                if (i.spr && active() && S.dn) {
                  const cm = R.getParameter(g.COLOR_WRITEMASK);
                  if (!cm[0] && !cm[1] && !cm[2] && !cm[3]) return;
                }
                if (i.has3 || i.hasM) {
                  if (!i.seeded) {
                    // read once: the game may never upload these again
                    i.seeded = true;
                    touched.add(curProg);
                    for (const k in i.v3)
                      if (!i.orig[k])
                        i.orig[k] = Array.from(R.getUniform(curProg, i.v3[k]));
                    for (const k in i.m4)
                      if (k.indexOf("sun") === 0) {
                        const r =
                          i.rec[k] ||
                          (i.rec[k] = {
                            orig: null,
                            po: null,
                          });
                        if (!r.orig)
                          r.orig = Float32Array.from(
                            R.getUniform(curProg, i.m4[k]),
                          );
                      }
                    i.sig = "";
                    i.msig = "";
                  }
                  if (i.sig !== curSig) {
                    i.sig = curSig;
                    for (const k in i.v3)
                      R.uniform3fv(
                        i.v3[k],
                        Float32Array.from(modVec(k, i.orig[k])),
                      );
                  }
                  if (i.msig !== curSig) {
                    i.msig = curSig;
                    writeMats(i, false);
                  }
                }
                if (i.depth && i.origP && i.dsig !== curSig) {
                  i.dsig = curSig;
                  R.uniformMatrix4fv(
                    i.m4.projectionMatrix,
                    false,
                    Float32Array.from(modDepthProj(i.origP)),
                  );
                }
              }
              return prev.apply(this, arguments);
            },
        ),
      );
    };
    const uninstall = () => {
      // put the game's own values back, then stop wrapping anything
      for (const p of touched) {
        const i = info.get(p);
        if (!i) continue;
        try {
          R.useProgram(p);
          for (const k in i.v3)
            if (i.orig[k]) R.uniform3fv(i.v3[k], Float32Array.from(i.orig[k]));
          writeMats(i, true);
          if (i.depth && i.origP)
            R.uniformMatrix4fv(i.m4.projectionMatrix, false, i.origP);
        } catch (e) {}
      }
      hooks
        .splice(0)
        .reverse()
        .forEach((f) => f());
      installed = false;
      touched.clear();
      P.vec = null;
      S.dn = null;
    };
    const timer = setInterval(() => {
      try {
        curSig = sigOf();
        if (P.enabled && !installed) install();
        else if (!P.enabled && installed) {
          const pr = R.getParameter(g.CURRENT_PROGRAM);
          uninstall();
          R.useProgram(pr);
        }
      } catch (e) {
        S.err = String(e);
      }
    }, 250);
    return {
      S,
      destroy() {
        clearInterval(timer);
        if (installed) {
          const pr = R.getParameter(g.CURRENT_PROGRAM);
          uninstall();
          R.useProgram(pr);
        }
      },
    };
  }




  // ======================= SKYBOX CHANGER =======================
  // The game draws its sky as six textured quads (one per cube face). The faces of every skybox the game has downloaded sit in its own
  // IndexedDB cache ("protanki" / "resources", 1BNT bundles holding image.jpg). Choosing a sky builds six textures from those images and swaps
  // them in while the game draws the sky quads. The game's own textures and data are never touched, so "Map default" simply stops swapping.
  const SKY = {
    set: "",
  }; // '' = the map's own sky, otherwise the key of a set from SKY_SETS (the smallest face id of the set)
  // Faces per set in the order [-X, +X, +Z, -Z, -Y, +Y] (cached image resource ids). The order was solved by matching the edges of the images
  // to the geometry of the game's sky box, so the seams line up (checked against the sky the game itself was drawing).
  const SKY_SETS = [
    ["Sunset Blaze", [358, 357, 360, 359, 356, 355]],
    ["Dusty Storm", [364, 363, 366, 365, 362, 361]],
    ["Overcast Sun", [370, 369, 372, 371, 368, 367]],
    ["Tropical Sea", [1356, 1355, 1352, 1354, 1353, 1357]],
    ["Cloudy Haze", [1362, 1361, 1358, 1360, 1359, 1363]],
    ["Moonlit Sea", [1367, 1368, 1366, 1364, 1365, 1369]],
    ["Midnight Haze", [1373, 1374, 1372, 1370, 1371, 1375]],
    ["Blocky World", [1380, 1379, 1376, 1378, 1377, 1381]],
    ["Beach Mountains", [1384, 1382, 1386, 1385, 1383, 1387]],
    ["Meadow Day", [1392, 1391, 1388, 1390, 1389, 1393]],
    ["Meadow Day II", [1398, 1397, 1394, 1396, 1395, 1399]],
    ["Night City", [1502, 1400, 1504, 1503, 1401, 1505]],
    ["Deep Blue Dusk", [1510, 1509, 1506, 1508, 1507, 1511]],
    ["Desert Dunes", [1516, 1515, 1512, 1514, 1513, 1517]],
    ["Snow Plains", [1522, 1521, 1518, 1520, 1519, 1523]],
    ["Dry Steppe", [1526, 1524, 1528, 1527, 1525, 1529]],
    ["Neon Lights", [1534, 1533, 1530, 1532, 1531, 1535]],
    ["Dusk Hills", [1536, 1538, 1539, 1540, 1537, 1541]],
    ["Night Forest", [1546, 1545, 1542, 1544, 1543, 1547]],
    ["Night Plains", [1551, 1702, 1550, 1548, 1549, 1703]],
    ["Starry Meadow", [1707, 1708, 1706, 1704, 1705, 1709]],
    ["Crimson Night", [1714, 1713, 1710, 1712, 1711, 1715]],
    ["Sunset Blaze II", [1720, 1719, 1716, 1718, 1717, 1721]],
    ["Nebula", [1726, 1725, 1722, 1724, 1723, 1727]],
    ["Winter Sky", [1732, 1731, 1728, 1730, 1729, 1733]],
    ["Cosmic Aurora", [1738, 1737, 1734, 1736, 1739, 1735]],
    ["Dark Space", [7263, 7264, 7259, 7262, 7261, 7265]],
    ["Fire Sky", [7865, 7864, 7862, 7867, 7866, 7863]],
    ["Polar Aurora", [8609, 8608, 8605, 8607, 8606, 8610]],
    ["Cartoon Desert", [11004, 11006, 11007, 11008, 11005, 11009]],
    ["Deep Space", [11026, 11027, 11025, 11028, 11030, 11029]],
    ["Underwater", [14262, 14260, 14264, 14263, 14261, 14265]],
    ["Lunar Night", [16640, 16639, 16636, 16638, 16637, 16641]],
  ];
  // The cache and the catalogue. Needs no game canvas, so the menu can show thumbnails at any time.
  const SKYLIB = (function () {
    const sets = SKY_SETS.map((s) => ({
      name: s[0],
      faces: s[1],
      key: String(Math.min.apply(null, s[1])),
    }));
    let dbP = null;
    // Never open the game's database unless it already exists: opening a missing one would create an empty copy that the game then cannot use.
    const openDb = () =>
      dbP ||
      (dbP = (async () => {
        try {
          if (!indexedDB.databases) return null;
          const list = await indexedDB.databases();
          if (!list.some((d) => d.name === "protanki")) return null;
          return await new Promise((res) => {
            const r = indexedDB.open("protanki");
            r.onsuccess = () => {
              const db = r.result;
              if (!db.objectStoreNames.contains("resources")) {
                try {
                  db.close();
                } catch (e) {}
                res(null);
                return;
              }
              db.onversionchange = () => {
                try {
                  db.close();
                } catch (e) {}
                dbP = null;
              };
              db.onclose = () => {
                dbP = null;
              };
              res(db);
            };
            r.onerror = () => res(null);
            r.onblocked = () => res(null);
            r.onupgradeneeded = () => {
              try {
                r.transaction.abort();
              } catch (e) {}
            };
          });
        } catch (e) {
          return null;
        }
      })().then((db) => {
        if (!db) dbP = null;
        return db;
      }));
    const store = (db) => db.transaction("resources").objectStore("resources");
    const getRec = (id) =>
      openDb().then((db) =>
        !db
          ? null
          : new Promise((res) => {
              try {
                const q = store(db).get(id + ":default");
                q.onsuccess = () => res(q.result || null);
                q.onerror = () => res(null);
              } catch (e) {
                res(null);
              }
            }),
      );
    const allKeys = () =>
      openDb().then((db) =>
        !db
          ? []
          : new Promise((res) => {
              try {
                const q = store(db).getAllKeys();
                q.onsuccess = () => res(q.result || []);
                q.onerror = () => res([]);
              } catch (e) {
                res([]);
              }
            }),
      );
    // 1BNT bundle: "1BNT", u32 file count, then per file: u32 name length, name, u32 data length, data. The picture is the file called image.*
    const unpack = (buf) => {
      try {
        const u = new Uint8Array(buf);
        if (
          u.length < 12 ||
          u[0] !== 0x31 ||
          u[1] !== 0x42 ||
          u[2] !== 0x4e ||
          u[3] !== 0x54
        )
          return null;
        const dv = new DataView(u.buffer, u.byteOffset, u.byteLength);
        const n = dv.getUint32(4, true);
        let p = 8;
        for (let i = 0; i < n && p + 4 <= u.length; i++) {
          const nl = dv.getUint32(p, true);
          p += 4;
          const name = new TextDecoder().decode(u.subarray(p, p + nl));
          p += nl;
          const dl = dv.getUint32(p, true);
          p += 4;
          if (/^image\.(jpe?g|png)$/i.test(name)) return u.subarray(p, p + dl);
          p += dl;
        }
      } catch (e) {}
      return null;
    };
    // decoded picture of one cached face (optionally shrunk to size x size)
    const bitmap = async (id, size) => {
      let blob;
      if (id instanceof Blob)
        blob = id; // a custom face
      else {
        const rec = await getRec(id);
        if (!rec || !(rec.data instanceof ArrayBuffer)) return null;
        const img = unpack(rec.data);
        if (!img) return null;
        blob = new Blob([img]);
      }
      try {
        return await (size
          ? createImageBitmap(blob, {
              resizeWidth: size,
              resizeHeight: size,
              resizeQuality: "medium",
              premultiplyAlpha: "none",
            })
          : createImageBitmap(blob, {
              premultiplyAlpha: "none",
            }));
      } catch (e) {
        return null;
      }
    };
    let availP = null;
    const avail = (force) =>
      (
        (!force && availP) ||
        (availP = allKeys().then((ks) => {
          const have = new Set(ks.map(String));
          return sets.filter((s) =>
            s.faces.every((id) => have.has(id + ":default")),
          );
        }))
      ).then((list) => loadCustoms().then(() => customs.concat(list))); // custom skies first
    const thumbs = new Map();
    let queue = Promise.resolve();
    const thumb = (s) => {
      // 96x54 preview: a band of one side face around the horizon, plus the top and bottom faces
      let p = thumbs.get(s.key);
      if (p) return p;
      const make = async () => {
        const W = 96,
          H = 54,
          cv = document.createElement("canvas");
        cv.width = W;
        cv.height = H;
        const c = cv.getContext("2d");
        c.fillStyle = "#0c0d12";
        c.fillRect(0, 0, W, H);
        const side = await bitmap(s.faces[2], 128),
          top = await bitmap(s.faces[5], 32),
          bot = await bitmap(s.faces[4], 32);
        if (side) {
          const bh = (side.width * H) / 72,
            sy = Math.max(0, (side.height - bh) / 2);
          c.drawImage(
            side,
            0,
            sy,
            side.width,
            Math.min(bh, side.height),
            0,
            0,
            72,
            H,
          );
          side.close();
        }
        if (top) {
          c.drawImage(top, 73, 0, 23, 26);
          top.close();
        }
        if (bot) {
          c.drawImage(bot, 73, 28, 23, 26);
          bot.close();
        }
        return cv;
      };
      p = queue = queue.then(make, make); // one at a time
      thumbs.set(s.key, p);
      return p;
    };
    // ---- custom skies: one 2:1 panorama, or six face pictures. Stored as six small pictures in this script's own database ("postfx-sky"),
    // already turned into the game's face order and orientation, so they are used exactly like the cached ones.
    const customs = [];
    let customsReady = false,
      customsP = null,
      cdbP = null;
    const cdb = () =>
      cdbP ||
      (cdbP = new Promise((res) => {
        try {
          const r = indexedDB.open("postfx-sky", 1);
          r.onupgradeneeded = () => {
            try {
              r.result.createObjectStore("custom", {
                keyPath: "id",
              });
            } catch (e) {}
          };
          r.onsuccess = () => res(r.result);
          r.onerror = () => res(null);
          r.onblocked = () => res(null);
        } catch (e) {
          res(null);
        }
      }));
    const call = (mk) =>
      cdb().then((db) =>
        !db
          ? null
          : new Promise((res) => {
              try {
                const q = mk(
                  db.transaction("custom", "readwrite").objectStore("custom"),
                );
                q.onsuccess = () =>
                  res(q.result === undefined ? true : q.result);
                q.onerror = () => res(null);
              } catch (e) {
                res(null);
              }
            }),
      );
    const loadCustoms = (force) =>
      (!force && customsP) ||
      (customsP = call((st) => st.getAll()).then((list) => {
        customs.length = 0;
        for (const r of Array.isArray(list) ? list : [])
          if (r && r.faces && r.faces.length === 6)
            customs.push({
              name: r.name,
              faces: r.faces,
              key: "c:" + r.id,
              id: r.id,
              custom: true,
              added: r.added || 0,
            });
        customs.sort((a, b) => b.added - a.added);
        customsReady = true;
      }));
    loadCustoms();
    // geometry of the game's sky (see installSky): corners A, B, C, D per slot [-X, +X, +Z, -Z, -Y, +Y]; picture pixel (u, v) lies at A + u (D - A) + v (B - A)
    const CORN = [
      [
        [-1, 1, 1],
        [-1, -1, 1],
        [-1, -1, -1],
        [-1, 1, -1],
      ],
      [
        [1, 1, -1],
        [1, -1, -1],
        [1, -1, 1],
        [1, 1, 1],
      ],
      [
        [1, 1, 1],
        [1, -1, 1],
        [-1, -1, 1],
        [-1, 1, 1],
      ],
      [
        [-1, 1, -1],
        [-1, -1, -1],
        [1, -1, -1],
        [1, 1, -1],
      ],
      [
        [-1, -1, -1],
        [-1, -1, 1],
        [1, -1, 1],
        [1, -1, -1],
      ],
      [
        [-1, 1, 1],
        [-1, 1, -1],
        [1, 1, -1],
        [1, 1, 1],
      ],
    ];
    const EDGE = {
      top: [0, 3],
      bottom: [1, 2],
      left: [0, 1],
      right: [3, 2],
    }; // corner pairs; pixels run from the first to the second
    const seams = (function () {
      const m = {},
        key = (p) => p.join(",");
      CORN.forEach((c, s) => {
        for (const e in EDGE) {
          let P = c[EDGE[e][0]],
            Q = c[EDGE[e][1]],
            rev = false;
          if (key(P) > key(Q)) {
            const t = P;
            P = Q;
            Q = t;
            rev = true;
          }
          (m[key(P) + "|" + key(Q)] = m[key(P) + "|" + key(Q)] || []).push({
            s,
            e,
            rev,
          });
        }
      });
      return Object.keys(m).map((k) => m[k]);
    })();
    const perms = [];
    (function rec(a, rest) {
      if (!rest.length) {
        perms.push(a);
        return;
      }
      rest.forEach((x, i) =>
        rec(
          a.concat(x),
          rest.filter((_, j) => j !== i),
        ),
      );
    })([], [0, 1, 2, 3, 4, 5]);
    const N = 128;
    const stripsOf = (bmp) => {
      // edge strips of a picture in each of its 4 quarter turns
      const cv = document.createElement("canvas");
      cv.width = cv.height = N;
      const c = cv.getContext("2d", {
          willReadFrequently: true,
        }),
        out = [];
      for (let r = 0; r < 4; r++) {
        c.setTransform(1, 0, 0, 1, 0, 0);
        c.clearRect(0, 0, N, N);
        c.translate(N / 2, N / 2);
        c.rotate((r * Math.PI) / 2);
        c.imageSmoothingQuality = "high";
        c.drawImage(bmp, -N / 2, -N / 2, N, N);
        const d = c.getImageData(0, 0, N, N).data,
          S = {
            top: [],
            bottom: [],
            left: [],
            right: [],
          };
        const px = (x, y, o) => {
          const p = (y * N + x) * 4;
          return d[p + o];
        };
        for (let i = 0; i < N; i++)
          for (const [e, x0, y0, x1, y1] of [
            ["top", i, 0, i, 1],
            ["bottom", i, N - 1, i, N - 2],
            ["left", 0, i, 1, i],
            ["right", N - 1, i, N - 2, i],
          ])
            S[e].push(
              [0, 1, 2].map((o) => (px(x0, y0, o) + px(x1, y1, o)) / 2),
            );
        out.push(S);
      }
      return out;
    };
    const edgeDist = (A, B, r1, r2) => {
      let t = 0;
      for (let i = 0; i < N; i++) {
        const a = A[r1 ? N - 1 - i : i],
          b = B[r2 ? N - 1 - i : i];
        t +=
          Math.abs(a[0] - b[0]) + Math.abs(a[1] - b[1]) + Math.abs(a[2] - b[2]);
      }
      return t / (N * 3);
    };
    // pm[slot] = picture index, rots = quarter turns of the top / bottom pictures (slots 4, 5); returns the seam error
    const seamCost = (S, pm, rots) => {
      let c = 0;
      for (const sm of seams) {
        const a = sm[0],
          b = sm[1];
        c += edgeDist(
          S[pm[a.s]][a.s > 3 ? rots[a.s - 4] : 0][a.e],
          S[pm[b.s]][b.s > 3 ? rots[b.s - 4] : 0][b.e],
          a.rev,
          b.rev,
        );
      }
      return c / 12;
    };
    const bestRots = (S, pm) => {
      let best = null;
      for (let r4 = 0; r4 < 4; r4++)
        for (let r5 = 0; r5 < 4; r5++) {
          const c = seamCost(S, pm, [r4, r5]);
          if (!best || c < best.cost)
            best = {
              cost: c,
              rots: [r4, r5],
            };
        }
      return best;
    };
    // Which picture goes where, and how top / bottom must be turned. A hint (from the file names) wins unless the seams clearly disagree.
    const orient = (bmps, hint) => {
      const S = bmps.map(stripsOf);
      let best = null;
      for (const pm of perms) {
        const b = bestRots(S, pm);
        if (!best || b.cost < best.cost)
          best = {
            pm,
            cost: b.cost,
            rots: b.rots,
          };
      }
      if (hint) {
        const b = bestRots(S, hint);
        if (b.cost <= best.cost * 1.6 + 3)
          return {
            pm: hint,
            rots: b.rots,
            cost: b.cost,
          };
      }
      return best;
    };
    const ROLES = [
      ["front", /(^|[^a-z0-9])(front|ft|pz|posz|forward)([^a-z0-9]|$)/],
      ["back", /(^|[^a-z0-9])(back|bk|nz|negz|rear)([^a-z0-9]|$)/],
      ["left", /(^|[^a-z0-9])(left|lf|nx|negx)([^a-z0-9]|$)/],
      ["right", /(^|[^a-z0-9])(right|rt|px|posx)([^a-z0-9]|$)/],
      ["up", /(^|[^a-z0-9])(up|top|py|posy|sky)([^a-z0-9]|$)/],
      ["down", /(^|[^a-z0-9])(down|dn|bottom|bt|ny|negy|floor)([^a-z0-9]|$)/],
    ];
    const ROLE_SLOT = {
      front: 2,
      right: 0,
      back: 3,
      left: 1,
      up: 5,
      down: 4,
    }; // seen from inside: front, right, back, left run left to right
    const hintOf = (names) => {
      const pm = [];
      for (let i = 0; i < names.length; i++) {
        const n = names[i].toLowerCase().replace(/\.[a-z0-9]+$/, "");
        const r = ROLES.find((x) => x[1].test(n));
        if (!r || pm[ROLE_SLOT[r[0]]] !== undefined) return null;
        pm[ROLE_SLOT[r[0]]] = i;
      }
      return pm.length === 6 && pm.every((v) => v !== undefined) ? pm : null;
    };
    const toBlob = (cv) =>
      new Promise((res, rej) =>
        cv.toBlob(
          (b) => (b ? res(b) : rej(new Error("could not save a face"))),
          "image/jpeg",
          0.92,
        ),
      );
    const faceBlob = (bmp, rot) => {
      // square picture, at most 1024 px, turned by rot quarter turns
      const sz = Math.max(16, Math.min(1024, Math.max(bmp.width, bmp.height))),
        cv = document.createElement("canvas");
      cv.width = cv.height = sz;
      const c = cv.getContext("2d");
      c.translate(sz / 2, sz / 2);
      c.rotate((rot * Math.PI) / 2);
      c.imageSmoothingQuality = "high";
      c.drawImage(bmp, -sz / 2, -sz / 2, sz, sz);
      return toBlob(cv);
    };
    const panoFaces = async (bmp) => {
      // equirectangular panorama -> six faces. Picture centre looks along +Z, moving right turns towards -X (as in the game's own skies)
      const W = Math.min(bmp.width, 4096),
        H = Math.round((W * bmp.height) / bmp.width),
        pc = document.createElement("canvas");
      pc.width = W;
      pc.height = H;
      const pcx = pc.getContext("2d", {
        willReadFrequently: true,
      });
      pcx.drawImage(bmp, 0, 0, W, H);
      const src = pcx.getImageData(0, 0, W, H).data;
      const sz = Math.max(
          256,
          Math.min(1024, 1 << Math.floor(Math.log2(Math.max(256, W / 4)))),
        ),
        out = [];
      const fc = document.createElement("canvas");
      fc.width = fc.height = sz;
      const fx = fc.getContext("2d", {
        willReadFrequently: true,
      });
      for (let s = 0; s < 6; s++) {
        const [A, B, , D] = CORN[s],
          img = fx.createImageData(sz, sz),
          o = img.data;
        for (let y = 0; y < sz; y++)
          for (let x = 0; x < sz; x++) {
            const u = (x + 0.5) / sz,
              v = (y + 0.5) / sz;
            const px = A[0] + u * (D[0] - A[0]) + v * (B[0] - A[0]),
              py = A[1] + u * (D[1] - A[1]) + v * (B[1] - A[1]),
              pz = A[2] + u * (D[2] - A[2]) + v * (B[2] - A[2]);
            const l = Math.hypot(px, py, pz),
              fu = (0.5 + Math.atan2(-px, pz) / (2 * Math.PI)) * W - 0.5,
              fv = Math.max(
                0,
                Math.min(H - 1, (0.5 - Math.asin(py / l) / Math.PI) * H - 0.5),
              );
            const x0 = Math.floor(fu),
              y0 = Math.floor(fv),
              tx = fu - x0,
              ty = fv - y0,
              xa = ((x0 % W) + W) % W,
              xb = (xa + 1) % W,
              yb = Math.min(H - 1, y0 + 1),
              i = (y * sz + x) * 4;
            for (let k = 0; k < 3; k++)
              o[i + k] =
                (src[(y0 * W + xa) * 4 + k] * (1 - tx) +
                  src[(y0 * W + xb) * 4 + k] * tx) *
                  (1 - ty) +
                (src[(yb * W + xa) * 4 + k] * (1 - tx) +
                  src[(yb * W + xb) * 4 + k] * tx) *
                  ty;
            o[i + 3] = 255;
          }
        fx.putImageData(img, 0, 0);
        out.push(await toBlob(fc));
      }
      return out;
    };
    const nameOf = (files) => {
      let n =
        files.length === 1
          ? files[0].name
          : files[0].name
              .replace(/\.[^.]+$/, "")
              .replace(
                /[_\-\s.]*(front|back|left|right|up|down|top|bottom|ft|bk|lf|rt|dn|px|nx|py|ny|pz|nz)[_\-\s.]*/gi,
                " ",
              )
              .trim() || "Custom sky";
      n =
        n
          .replace(/\.[^.]+$/, "")
          .replace(/[_]+/g, " ")
          .trim()
          .slice(0, 28) || "Custom sky";
      let name = n,
        i = 2;
      while (customs.some((c) => c.name === name)) name = n + " " + i++;
      return name;
    };
    const add = async (files) => {
      files = Array.from(files).filter(
        (f) =>
          /^image\//.test(f.type) ||
          /\.(jpe?g|png|webp|bmp|gif)$/i.test(f.name),
      );
      if (files.length !== 1 && files.length !== 6)
        throw new Error("Pick one 2:1 panorama or six face pictures");
      await loadCustoms();
      const bmps = [];
      let faces;
      try {
        for (const f of files) {
          try {
            bmps.push(await createImageBitmap(f));
          } catch (e) {
            throw new Error("Could not read " + f.name);
          }
        }
        if (files.length === 1) {
          const r = bmps[0].width / bmps[0].height;
          if (r < 1.7 || r > 2.3)
            throw new Error(
              "A single picture must be a 2:1 panorama (equirectangular)",
            );
          faces = await panoFaces(bmps[0]);
        } else {
          const o = orient(bmps, hintOf(files.map((f) => f.name)));
          faces = [];
          for (let s = 0; s < 6; s++)
            faces.push(
              await faceBlob(bmps[o.pm[s]], s > 3 ? o.rots[s - 4] : 0),
            );
        }
      } finally {
        for (const b of bmps) {
          try {
            b.close();
          } catch (e) {}
        }
      }
      const rec = {
        id: Date.now().toString(36) + Math.random().toString(36).slice(2, 6),
        name: nameOf(files),
        faces,
        added: Date.now(),
      };
      if (!(await call((st) => st.put(rec))))
        throw new Error("Could not save the sky in this browser");
      await loadCustoms(true);
      availP = null;
      return {
        key: "c:" + rec.id,
        name: rec.name,
      };
    };
    const remove = async (key) => {
      const c = customs.find((x) => x.key === key);
      if (!c) return;
      await call((st) => st.delete(c.id));
      thumbs.delete(key);
      await loadCustoms(true);
      availP = null;
    };
    return {
      sets,
      byKey: (k) =>
        sets.find((s) => s.key === k) ||
        customs.find((s) => s.key === k) ||
        null,
      customsReady: () => customsReady,
      avail,
      bitmap,
      thumb,
      add,
      remove,
    };
  })();

  function installSky(game) {
    const g = game.getContext("webgl2");
    const PR = WebGL2RenderingContext.prototype;
    const Q = (n, ...a) => PR[n].apply(g, a); // straight to the browser, so none of the other modules' wrappers see these calls
    const S = {
      want: null,
      token: 0,
      tex: null,
      err: "",
      swaps: 0,
      faces: 0,
      rejected: 0,
      retryAt: 0,
      failed: null,
    };
    // Corners (A, B, C, D) of every face in the game's own sky mesh, in slot order [-X, +X, +Z, -Z, -Y, +Y]. A triangle pair is A B C, A C D with
    // texture coordinates A=(0,0) B=(0,1) C=(1,1) D=(1,0). A face is only swapped when the game's quad matches this exactly.
    const CORN = [
      [
        [-1, 1, 1],
        [-1, -1, 1],
        [-1, -1, -1],
        [-1, 1, -1],
      ],
      [
        [1, 1, -1],
        [1, -1, -1],
        [1, -1, 1],
        [1, 1, 1],
      ],
      [
        [1, 1, 1],
        [1, -1, 1],
        [-1, -1, 1],
        [-1, 1, 1],
      ],
      [
        [-1, 1, -1],
        [-1, -1, -1],
        [1, -1, -1],
        [1, 1, -1],
      ],
      [
        [-1, -1, -1],
        [-1, -1, 1],
        [1, -1, 1],
        [1, -1, -1],
      ],
      [
        [-1, 1, 1],
        [-1, 1, -1],
        [1, 1, -1],
        [1, 1, 1],
      ],
    ];
    const SEQ = [0, 1, 2, 0, 2, 3],
      UVS = [
        [0, 0],
        [0, 1],
        [1, 1],
        [0, 0],
        [1, 1],
        [1, 0],
      ];
    const progs = new WeakMap(),
      faceOf = new WeakMap();
    const probeProg = (p) => {
      const pi = {
        sky: false,
        unit: 0,
      };
      try {
        let fog = false,
          sampler = null;
        const n = Q("getProgramParameter", p, g.ACTIVE_UNIFORMS);
        for (let i = 0; i < n; i++) {
          const u = Q("getActiveUniform", p, i);
          if (u.name === "fogSkyAlpha") fog = true;
          if (u.type === g.SAMPLER_2D && (u.name === "map" || !sampler))
            sampler = u.name;
        }
        if (
          fog &&
          sampler &&
          Q("getAttribLocation", p, "position") >= 0 &&
          Q("getAttribLocation", p, "uv") >= 0
        ) {
          const v = Q("getUniform", p, Q("getUniformLocation", p, sampler));
          pi.unit = typeof v === "number" && v >= 0 && v < 16 ? v : 0;
          pi.sky = true;
        }
      } catch (e) {}
      return pi;
    };
    const readAttr = (p, name, comps) => {
      // the first six vertices of an attribute, read back from its buffer once
      const loc = Q("getAttribLocation", p, name);
      if (loc < 0) return null;
      const buf = Q(
        "getVertexAttrib",
        loc,
        g.VERTEX_ATTRIB_ARRAY_BUFFER_BINDING,
      );
      if (!buf) return null;
      if (
        Q("getVertexAttrib", loc, g.VERTEX_ATTRIB_ARRAY_TYPE) !== g.FLOAT ||
        Q("getVertexAttrib", loc, g.VERTEX_ATTRIB_ARRAY_SIZE) < comps
      )
        return null;
      const stride =
          Q("getVertexAttrib", loc, g.VERTEX_ATTRIB_ARRAY_STRIDE) || comps * 4,
        off = Q("getVertexAttribOffset", loc, g.VERTEX_ATTRIB_ARRAY_POINTER);
      const need = off + stride * 5 + comps * 4,
        prevB = Q("getParameter", g.COPY_READ_BUFFER_BINDING);
      let out = null;
      try {
        Q("bindBuffer", g.COPY_READ_BUFFER, buf);
        if (
          Q("getBufferParameter", g.COPY_READ_BUFFER, g.BUFFER_SIZE) >= need
        ) {
          const bytes = new Uint8Array(need);
          Q("getBufferSubData", g.COPY_READ_BUFFER, 0, bytes);
          const dv = new DataView(bytes.buffer);
          out = [];
          for (let v = 0; v < 6; v++) {
            const o = [];
            for (let k = 0; k < comps; k++)
              o.push(dv.getFloat32(off + v * stride + k * 4, true));
            out.push(o);
          }
        }
      } finally {
        Q("bindBuffer", g.COPY_READ_BUFFER, prevB);
      }
      return out;
    };
    const classify = (p) => {
      // which slot (0..5) is the quad being drawn, or -1 when it is not one of the sky faces
      try {
        const pos = readAttr(p, "position", 3),
          uv = readAttr(p, "uv", 2);
        if (!pos || !uv) return -1;
        let m = 0;
        for (const v of pos) for (const x of v) m = Math.max(m, Math.abs(x));
        if (m < 100) return -1;
        for (let i = 0; i < 6; i++) {
          if (
            Math.abs(uv[i][0] - UVS[i][0]) > 0.02 ||
            Math.abs(uv[i][1] - UVS[i][1]) > 0.02
          )
            return -1;
        }
        for (let s = 0; s < 6; s++) {
          let ok = true;
          for (let i = 0; i < 6 && ok; i++) {
            const c = CORN[s][SEQ[i]];
            for (let k = 0; k < 3; k++)
              if (Math.abs(pos[i][k] - c[k] * m) > m * 0.01) {
                ok = false;
                break;
              }
          }
          if (ok) return s;
        }
      } catch (e) {
        S.err = "classify: " + e;
      }
      return -1;
    };
    // ---- one-draw swap: bind our texture for the sky quad, draw, put the game's binding back
    const swapIn = () => {
      const p = Q("getParameter", g.CURRENT_PROGRAM);
      if (!p) return null;
      let pi = progs.get(p);
      if (!pi) {
        pi = probeProg(p);
        progs.set(p, pi);
      }
      if (!pi.sky) return null;
      const at = Q("getParameter", g.ACTIVE_TEXTURE);
      Q("activeTexture", g.TEXTURE0 + pi.unit);
      const orig = Q("getParameter", g.TEXTURE_BINDING_2D);
      let slot = -1;
      if (orig) {
        slot = faceOf.get(orig);
        if (slot === undefined) {
          slot = classify(p);
          faceOf.set(orig, slot);
          if (slot >= 0) S.faces++;
          else S.rejected++;
        }
      }
      const t = slot >= 0 && S.tex ? S.tex[slot] : null;
      if (!t) {
        Q("activeTexture", at);
        return null;
      }
      Q("bindTexture", g.TEXTURE_2D, t);
      S.swaps++;
      return () => {
        Q("bindTexture", g.TEXTURE_2D, orig);
        Q("activeTexture", at);
      };
    };
    const own = Object.prototype.hasOwnProperty.call(g, "drawArrays"),
      prevDraw = g.drawArrays;
    const mine = function (mode, first, count) {
      if (S.tex && mode === 4 && count === 6) {
        let undo = null;
        try {
          undo = swapIn();
        } catch (e) {
          S.err = "swap: " + e;
        }
        try {
          return prevDraw.apply(this, arguments);
        } finally {
          if (undo) {
            try {
              undo();
            } catch (e) {}
          }
        }
      }
      return prevDraw.apply(this, arguments);
    };
    g.drawArrays = mine;
    // ---- building the six textures of a set (decoded outside the frame, uploaded in one go, game state put back afterwards)
    const free = (texs) => {
      if (texs)
        for (const t of texs) {
          try {
            Q("deleteTexture", t);
          } catch (e) {}
        }
    };
    const build = async (set) => {
      const bmps = [];
      try {
        for (let i = 0; i < 6; i++) {
          const b = await SKYLIB.bitmap(set.faces[i]);
          if (!b)
            throw new Error("face " + set.faces[i] + " is not in the cache");
          bmps.push(b);
        }
        const texs = [],
          unit = g.TEXTURE0 + 7,
          at = Q("getParameter", g.ACTIVE_TEXTURE);
        Q("activeTexture", unit);
        const keep = Q("getParameter", g.TEXTURE_BINDING_2D);
        const fy = Q("getParameter", g.UNPACK_FLIP_Y_WEBGL),
          pm = Q("getParameter", g.UNPACK_PREMULTIPLY_ALPHA_WEBGL);
        const ext = g.getExtension("EXT_texture_filter_anisotropic");
        try {
          Q("pixelStorei", g.UNPACK_FLIP_Y_WEBGL, false);
          Q("pixelStorei", g.UNPACK_PREMULTIPLY_ALPHA_WEBGL, false); // the game's own sky textures are uploaded the same way
          for (const b of bmps) {
            const t = Q("createTexture");
            texs.push(t);
            Q("bindTexture", g.TEXTURE_2D, t);
            Q(
              "texImage2D",
              g.TEXTURE_2D,
              0,
              g.SRGB8_ALPHA8,
              g.RGBA,
              g.UNSIGNED_BYTE,
              b,
            ); // the game's sky is sRGB too
            Q("texParameteri", g.TEXTURE_2D, g.TEXTURE_WRAP_S, g.CLAMP_TO_EDGE);
            Q("texParameteri", g.TEXTURE_2D, g.TEXTURE_WRAP_T, g.CLAMP_TO_EDGE);
            Q("texParameteri", g.TEXTURE_2D, g.TEXTURE_MAG_FILTER, g.LINEAR);
            Q(
              "texParameteri",
              g.TEXTURE_2D,
              g.TEXTURE_MIN_FILTER,
              g.LINEAR_MIPMAP_LINEAR,
            );
            if (ext)
              Q(
                "texParameterf",
                g.TEXTURE_2D,
                ext.TEXTURE_MAX_ANISOTROPY_EXT,
                Math.min(
                  8,
                  Q("getParameter", ext.MAX_TEXTURE_MAX_ANISOTROPY_EXT) || 1,
                ),
              );
            Q("generateMipmap", g.TEXTURE_2D);
          }
        } finally {
          Q("pixelStorei", g.UNPACK_FLIP_Y_WEBGL, fy);
          Q("pixelStorei", g.UNPACK_PREMULTIPLY_ALPHA_WEBGL, pm);
          Q("bindTexture", g.TEXTURE_2D, keep);
          Q("activeTexture", at);
        }
        return texs;
      } finally {
        for (const b of bmps) {
          try {
            b.close();
          } catch (e) {}
        }
      }
    };
    // follows SKY.set (also when it is changed from the console or loaded from a preset)
    const timer = setInterval(() => {
      try {
        const want = SKY.set || "";
        if (want === S.want) return;
        if (want.slice(0, 2) === "c:" && !SKYLIB.customsReady()) return; // custom skies load a moment after the script starts
        if (S.retryAt && want === S.failed && performance.now() < S.retryAt)
          return; // the cache may still be filling while the game starts: try again in a few seconds
        S.retryAt = 0;
        S.want = want;
        const my = ++S.token;
        const set = want ? SKYLIB.byKey(want) : null;
        if (!set) {
          const old = S.tex;
          S.tex = null;
          free(old);
          S.err = want ? "unknown sky " + want : "";
          return;
        }
        build(set)
          .then((texs) => {
            if (my !== S.token) {
              free(texs);
              return;
            }
            const old = S.tex;
            S.tex = texs;
            free(old);
            S.err = "";
          })
          .catch((e) => {
            if (my === S.token) {
              S.err = String((e && e.message) || e);
              S.want = null;
              S.failed = want;
              S.retryAt = performance.now() + 4000;
            }
          }); // not in the cache (yet): keep the map's own sky and retry
      } catch (e) {
        S.err = String(e);
      }
    }, 150);
    return {
      S,
      destroy() {
        clearInterval(timer);
        S.token++;
        if (g.drawArrays === mine) {
          if (own) g.drawArrays = prevDraw;
          else delete g.drawArrays;
        }
        const old = S.tex;
        S.tex = null;
        free(old);
      },
    };
  }




  // ======================= supervisor: attach to the main game canvas, re-attach if it is replaced =======================
  let current = null,
    cam = null,
    bloomOff = null,
    sunMod = null,
    skyMod = null;
  const pickCanvas = () => {
    let best = null,
      area = 0;
    for (const c of document.querySelectorAll("canvas")) {
      if (
        c.dataset.pfxOverlay ||
        c.className === "preview-canvas" ||
        c.width < 800 ||
        c.height < 400
      )
        continue;
      let ok = false;
      try {
        ok = !!c.getContext("webgl2");
      } catch (e) {}
      if (ok && c.width * c.height > area) {
        best = c;
        area = c.width * c.height;
      }
    }
    return best;
  };
  const detach = () => {
    try {
      bloomOff && bloomOff();
    } catch (e) {}
    try {
      cam && cam.destroy();
    } catch (e) {}
    try {
      sunMod && sunMod.destroy();
    } catch (e) {}
    try {
      skyMod && skyMod.destroy();
    } catch (e) {}
    bloomOff = cam = sunMod = skyMod = current = null;
  };
  const tick = () => {
    if (current && !current.isConnected) detach();
    if (current) return;
    const c = pickCanvas();
    if (!c) return;
    try {
      skyMod = installSky(c);
      cam = installCam(c);
      bloomOff = installBloom(c);
      sunMod = installSun(c);
      current = c;
      console.log("[postfx] attached");
    } catch (e) {
      console.error("[postfx] install failed", e);
      detach();
    }
  };
  setInterval(tick, 1000);




  // ======================= SETTINGS MENU (Insert to open; the key can be changed in General) =======================
  function installUI(S) {
    if (window.__postfxUI) {
      try {
        window.__postfxUI.destroy();
      } catch (e) {}
    }
    let uiReady = false;
    const animMeta = (animations && animations.GROUP_META) || {};
    const animGroups = Object.keys(animMeta);
    const ANIM = {
      activeGroup: animGroups[0] || "isida_dc",
      trigger: "shoot",
      hullTrigger: "always",
      speed: 1.0,
      intensity: 1.0,
      get activePreset() {
        return (
          this["preset_" + this.activeGroup] ||
          (animMeta[this.activeGroup] &&
            animMeta[this.activeGroup].defaultPreset) ||
          ""
        );
      },
      set activePreset(v) {
        this["preset_" + this.activeGroup] = v;
        if (animations) animations.setGroupPreset(this.activeGroup, v);
      },
    };
    for (const g of animGroups) {
      ANIM["preset_" + g] = animMeta[g].defaultPreset;
    }
    const animSync = () => {
      if (!animations) return;
      const presetsMap = {};
      for (const g of animGroups) presetsMap[g] = ANIM["preset_" + g];
      animations.setConfig({
        activeGroup: ANIM.activeGroup,
        trigger: ANIM.trigger,
        hullTrigger: ANIM.hullTrigger,
        speed: ANIM.speed,
        intensity: ANIM.intensity,
        presets: presetsMap,
      });
    };
    const O = {
      bloom: S.bloom,
      cam: S.cam,
      dof: S.dof,
      grade: S.grade,
      sharp: S.sharp,
      fog: S.fog,
      sun: S.sun,
      light: S.light,
      precip: S.precip,
      wet: S.wet,
      snowg: S.snowg,
      hud: S.hud,
      sky: S.sky,
      anim: ANIM,
    };
    const LSK = {
      settings: "postfx.ui.settings",
      presets: "postfx.ui.presets",
      preset: "postfx.ui.preset",
      quality: "ptpack.ui.quality",
      key: "postfx.ui.key",
      hostKey: "ptpack.ui.hostKey",
      tab: "ptpack.ui.tab",
      pos: "ptpack.ui.pos",
      keybinds: "ptpack.ui.keybinds",
    };
    const ls = {
      get(k, d) {
        try {
          const v = localStorage.getItem(k);
          return v == null ? d : JSON.parse(v);
        } catch (e) {
          return d;
        }
      },
      set(k, v) {
        try {
          localStorage.setItem(k, JSON.stringify(v));
        } catch (e) {}
      },
    };
    const clamp = (v, a, b) => Math.min(b, Math.max(a, v));
    const decimals = (s) => {
      const t = String(s);
      const i = t.indexOf(".");
      return i < 0 ? 0 : t.length - i - 1;
    };
    const isRain = () => O.precip.type === "rain",
      isSnow = () => O.precip.type === "snow";

    // ---------- what can be changed ----------
    const TABS = [
      {
        id: "weather",
        name: "Weather",
        icon: "cloud",
        group: "Effects",
      },
      {
        id: "camera",
        name: "Camera",
        icon: "sun",
      },
      {
        id: "color",
        name: "Color",
        icon: "palette",
      },
      {
        id: "atmos",
        name: "Atmosphere",
        icon: "bolt",
      },
      {
        id: "sky",
        name: "Skybox",
        icon: "sky",
      },
      ...(animGroups.length
        ? [
            {
              id: "anim",
              name: "Animations",
              icon: "anim",
            },
          ]
        : []),
      {
        id: "hud",
        name: "HUD",
        icon: "hud",
        group: "Interface",
      },
      {
        id: "presets",
        name: "Presets",
        icon: "bookmark",
        group: "System",
      },
      {
        id: "keybinds",
        name: "Keybinds",
        icon: "key",
      },
      {
        id: "general",
        name: "General",
        icon: "sliders",
      },
      {
        id: "logs",
        name: "Logs",
        icon: "logs",
      },
    ];
    const C = [];
    const sw = (tab, sec, col, label, o, k, extra) =>
      C.push(
        Object.assign(
          {
            tab,
            sec,
            col,
            label,
            t: "toggle",
            o,
            k,
          },
          extra,
        ),
      );
    const sl = (tab, sec, col, label, o, k, min, max, step, extra) =>
      C.push(
        Object.assign(
          {
            tab,
            sec,
            col,
            label,
            t: "slider",
            o,
            k,
            min,
            max,
            step,
          },
          extra,
        ),
      );
    const se = (tab, sec, col, label, o, k, opts, extra) =>
      C.push(
        Object.assign(
          {
            tab,
            sec,
            col,
            label,
            t: "select",
            o,
            k,
            opts,
          },
          extra,
        ),
      );
    const nt = (tab, sec, col, text) =>
      C.push({
        tab,
        sec,
        col,
        t: "note",
        text,
      });

    se("weather", "Precipitation", 0, "Type", "precip", "type", [
      ["off", "Off"],
      ["rain", "Rain"],
      ["snow", "Snow"],
    ]);
    sl(
      "weather",
      "Precipitation",
      0,
      "Amount",
      "precip",
      "count",
      0,
      30000,
      250,
      {
        en: () => O.precip.type !== "off",
      },
    );
    sl(
      "weather",
      "Precipitation",
      0,
      "Opacity",
      "precip",
      "alpha",
      0,
      1,
      0.01,
      {
        en: () => O.precip.type !== "off",
      },
    );
    sl("weather", "Precipitation", 0, "Haze", "precip", "mood", 0, 1, 0.01, {
      en: () => O.precip.type !== "off",
    });
    sl("weather", "Wind", 0, "Wind X", "precip", "windX", -2000, 2000, 50, {
      en: () => O.precip.type !== "off",
    });
    sl("weather", "Wind", 0, "Wind Z", "precip", "windZ", -2000, 2000, 50, {
      en: () => O.precip.type !== "off",
    });
    sl("weather", "Wind", 0, "Gusts", "precip", "gust", 0, 1, 0.01, {
      en: () => O.precip.type !== "off",
    });
    sl("weather", "Rain", 0, "Fall speed", "precip", "fall", 500, 9000, 100, {
      en: isRain,
    });
    sl("weather", "Rain", 0, "Streak length", "precip", "len", 20, 400, 5, {
      en: isRain,
    });
    sl("weather", "Rain", 0, "Streak width", "precip", "width", 0.3, 4, 0.1, {
      en: isRain,
    });
    sl("weather", "Snow", 0, "Fall speed", "precip", "snowFall", 40, 800, 10, {
      en: isSnow,
    });
    sl("weather", "Snow", 0, "Flake size", "precip", "snowSize", 1, 24, 0.5, {
      en: isSnow,
    });
    sl("weather", "Snow", 0, "Sway", "precip", "sway", 0, 500, 10, {
      en: isSnow,
    });

    const sgOn = () => O.snowg.enabled,
      stOn = () => O.snowg.enabled && O.snowg.tracks;
    sw("weather", "Snow ground", 0, "Enabled", "snowg", "enabled");
    sl("weather", "Snow ground", 0, "Coverage", "snowg", "cover", 0, 1, 0.01, {
      en: sgOn,
    });
    sl(
      "weather",
      "Snow ground",
      0,
      "Build-up time",
      "snowg",
      "build",
      0,
      900,
      10,
      {
        unit: " s",
        en: sgOn,
      },
    );
    sl("weather", "Snow ground", 0, "Melt time", "snowg", "melt", 5, 600, 5, {
      unit: " s",
      en: sgOn,
    });
    sl(
      "weather",
      "Snow ground",
      0,
      "Brightness",
      "snowg",
      "bright",
      0.4,
      1.6,
      0.01,
      {
        en: sgOn,
      },
    );
    C.push({
      tab: "weather",
      sec: "Snow ground",
      col: 0,
      label: "Cover the ground now",
      t: "button",
      btn: "Cover",
      fn: () => {
        O.snowg.level = 1;
      },
    });
    nt(
      "weather",
      "Snow ground",
      0,
      "Snow builds up on flat ground while the precipitation type is Snow (0 s build-up = at once) and melts when it stops.",
    );
    sw("weather", "Snow tracks", 0, "Enabled", "snowg", "tracks", {
      en: sgOn,
    });
    sl(
      "weather",
      "Snow tracks",
      0,
      "Depth",
      "snowg",
      "trackDepth",
      0,
      1,
      0.01,
      {
        en: stOn,
      },
    );
    sl(
      "weather",
      "Snow tracks",
      0,
      "Pushed-up snow",
      "snowg",
      "berm",
      0,
      2,
      0.05,
      {
        en: stOn,
      },
    );
    sl(
      "weather",
      "Snow tracks",
      0,
      "Tread pattern",
      "snowg",
      "pattern",
      0,
      1.5,
      0.05,
      {
        en: stOn,
      },
    );
    sl(
      "weather",
      "Snow tracks",
      0,
      "Fill-in time",
      "snowg",
      "fade",
      0,
      900,
      10,
      {
        unit: " s",
        en: stOn,
      },
    );
    C.push({
      tab: "weather",
      sec: "Snow tracks",
      col: 0,
      label: "Clear all tracks",
      t: "button",
      btn: "Clear",
      fn: () => {
        O.snowg.clearReq = 1;
      },
    });
    nt(
      "weather",
      "Snow tracks",
      0,
      "Tracks appear where a tank has touched the ground. Fill-in time 0 keeps them forever.",
    );
    sl(
      "weather",
      "Snow tracks advanced",
      0,
      "Snow range",
      "snowg",
      "range",
      3000,
      30000,
      500,
      {
        en: sgOn,
      },
    );
    sl(
      "weather",
      "Snow tracks advanced",
      0,
      "Height tolerance",
      "snowg",
      "contact",
      5,
      200,
      1,
      {
        en: stOn,
      },
    );
    se(
      "weather",
      "Snow tracks advanced",
      0,
      "Debug view",
      "snowg",
      "debug",
      [
        [0, "Off"],
        [1, "Tank mask"],
        [2, "Track map"],
      ],
      {
        vol: true,
      },
    );

    sw("weather", "Wet ground", 1, "Enabled", "wet", "enabled");
    sw(
      "weather",
      "Wet ground",
      1,
      "Keep puddles off tanks",
      "wet",
      "hideTanks",
      {
        en: () => O.wet.enabled,
        after: (v) => {
          try {
            localStorage.setItem("postfx.hideTanks", v ? "1" : "0");
          } catch (e) {}
        },
      },
    );
    sl("weather", "Wet ground", 1, "Wetness", "wet", "wet", 0, 1, 0.01, {
      en: () => O.wet.enabled,
    });
    sl("weather", "Wet ground", 1, "Puddles", "wet", "puddle", 0, 1, 0.01, {
      en: () => O.wet.enabled,
    });
    sl("weather", "Wet ground", 1, "Reflections", "wet", "refl", 0, 1, 0.01, {
      en: () => O.wet.enabled,
    });
    sl("weather", "Wet ground", 1, "Ripples", "wet", "ripple", 0, 1, 0.01, {
      en: () => O.wet.enabled,
    });
    nt(
      "weather",
      "Wet ground",
      1,
      "The wet ground is only drawn while the precipitation type is Rain.",
    );
    const spOn = () => O.wet.enabled && O.wet.splash;
    sw("weather", "Tank splashes", 1, "Enabled", "wet", "splash", {
      en: () => O.wet.enabled,
    });
    sl(
      "weather",
      "Tank splashes",
      1,
      "Ripple strength",
      "wet",
      "splashAmt",
      0,
      3,
      0.05,
      {
        en: spOn,
      },
    );
    sl("weather", "Tank splashes", 1, "Foam", "wet", "foam", 0, 2, 0.05, {
      en: spOn,
    });
    sl("weather", "Tank splashes", 1, "Wave speed", "wet", "wake", 40, 400, 5, {
      en: spOn,
    });
    sl(
      "weather",
      "Tank splashes",
      1,
      "Wave lifetime",
      "wet",
      "wakeLife",
      0.5,
      6,
      0.1,
      {
        unit: " s",
        en: spOn,
      },
    );
    nt(
      "weather",
      "Tank splashes",
      1,
      "Tanks that move through puddles push ripples and foam. A parked tank does not.",
    );
    sl(
      "weather",
      "Wet ground advanced",
      1,
      "Max distance",
      "wet",
      "maxDist",
      2000,
      20000,
      500,
      {
        en: () => O.wet.enabled,
      },
    );
    sl(
      "weather",
      "Wet ground advanced",
      1,
      "Ripple size",
      "wet",
      "cell",
      60,
      500,
      10,
      {
        en: () => O.wet.enabled,
      },
    );
    sl(
      "weather",
      "Wet ground advanced",
      1,
      "Ring speed",
      "wet",
      "speed",
      20,
      400,
      5,
      {
        en: () => O.wet.enabled,
      },
    );
    sl(
      "weather",
      "Wet ground advanced",
      1,
      "Sky smoothing",
      "wet",
      "envSmooth",
      0.02,
      2,
      0.01,
      {
        unit: " s",
        en: () => O.wet.enabled,
      },
    );
    se(
      "weather",
      "Wet ground advanced",
      1,
      "Debug view",
      "wet",
      "debug",
      [
        [0, "Off"],
        [1, "Tank mask"],
        [2, "Effect mask"],
        [3, "Splash waves"],
      ],
      {
        vol: true,
      },
    );

    sw("camera", "Bloom", 0, "Enabled", "bloom", "enabled");
    sl("camera", "Bloom", 0, "Threshold", "bloom", "threshold", 0, 1, 0.01, {
      en: () => O.bloom.enabled,
    });
    sl("camera", "Bloom", 0, "Soft knee", "bloom", "knee", 0, 1, 0.01, {
      en: () => O.bloom.enabled,
    });
    sl("camera", "Bloom", 0, "Intensity", "bloom", "intensity", 0, 3, 0.05, {
      en: () => O.bloom.enabled,
    });

    sw("camera", "Motion blur", 0, "Enabled", "cam", "enabled");
    sl("camera", "Motion blur", 0, "Strength", "cam", "strength", 0, 2, 0.05, {
      en: () => O.cam.enabled,
    });
    sl(
      "camera",
      "Motion blur",
      0,
      "Max length",
      "cam",
      "maxLen",
      0.005,
      0.15,
      0.005,
      {
        en: () => O.cam.enabled,
      },
    );
    sl("camera", "Motion blur", 0, "Samples", "cam", "samples", 4, 64, 1, {
      en: () => O.cam.enabled,
    });
    se(
      "camera",
      "Motion blur",
      0,
      "Debug view",
      "cam",
      "mode",
      [
        [0, "Off"],
        [1, "Depth"],
        [2, "Motion"],
      ],
      {
        vol: true,
      },
    );

    const dofOn = () => O.dof.enabled;
    sw("camera", "Depth of field", 1, "Enabled", "dof", "enabled");
    sw("camera", "Depth of field", 1, "Auto focus", "dof", "auto", {
      en: dofOn,
    });
    sl(
      "camera",
      "Depth of field",
      1,
      "Focus distance",
      "dof",
      "focus",
      100,
      20000,
      50,
      {
        en: () => O.dof.enabled && !O.dof.auto,
      },
    );
    sl(
      "camera",
      "Depth of field",
      1,
      "Focus height",
      "dof",
      "focusY",
      0,
      1,
      0.01,
      {
        en: () => O.dof.enabled && O.dof.auto,
      },
    );
    sl(
      "camera",
      "Depth of field",
      1,
      "Focus speed",
      "dof",
      "speed",
      0.5,
      20,
      0.5,
      {
        en: () => O.dof.enabled && O.dof.auto,
      },
    );
    sl(
      "camera",
      "Depth of field",
      1,
      "Blur strength",
      "dof",
      "strength",
      0,
      3,
      0.05,
      {
        en: dofOn,
      },
    );
    sl("camera", "Depth of field", 1, "Max blur", "dof", "maxR", 1, 24, 0.5, {
      unit: " px",
      en: dofOn,
    });
    sl(
      "camera",
      "Depth of field",
      1,
      "Sharp zone",
      "dof",
      "range",
      0,
      0.6,
      0.01,
      {
        en: dofOn,
      },
    );
    sl("camera", "Depth of field", 1, "Samples", "dof", "samples", 8, 48, 1, {
      en: dofOn,
    });
    se(
      "camera",
      "Depth of field",
      1,
      "Debug view",
      "dof",
      "mode",
      [
        [0, "Off"],
        [1, "Blur amount"],
        [2, "Focus"],
      ],
      {
        vol: true,
      },
    );

    const gOn = () => O.grade.enabled,
      shOn = () => O.sharp.enabled;
    sw("color", "Color grading", 0, "Enabled", "grade", "enabled");
    sl(
      "color",
      "Color grading",
      0,
      "Exposure",
      "grade",
      "expo",
      -1.5,
      1.5,
      0.05,
      {
        en: gOn,
      },
    );
    sl(
      "color",
      "Color grading",
      0,
      "Contrast",
      "grade",
      "contrast",
      0.5,
      1.6,
      0.01,
      {
        en: gOn,
      },
    );
    sl("color", "Color grading", 0, "Saturation", "grade", "sat", 0, 2, 0.01, {
      en: gOn,
    });
    sl(
      "color",
      "Color grading",
      0,
      "Temperature",
      "grade",
      "temp",
      -1,
      1,
      0.01,
      {
        en: gOn,
      },
    );
    sl("color", "Color grading", 0, "Tint", "grade", "tint", -1, 1, 0.01, {
      en: gOn,
    });
    sl("color", "Vignette", 1, "Strength", "grade", "vig", 0, 1, 0.01, {
      en: gOn,
    });
    sl("color", "Vignette", 1, "Size", "grade", "vigSize", 0.2, 1, 0.01, {
      en: gOn,
    });
    sw("color", "Sharpening", 1, "Enabled", "sharp", "enabled");
    sl("color", "Sharpening", 1, "Amount", "sharp", "amount", 0, 2, 0.05, {
      en: shOn,
    });
    sl(
      "color",
      "Sharpening",
      1,
      "Halo limit",
      "sharp",
      "limit",
      0.02,
      0.3,
      0.01,
      {
        en: shOn,
      },
    );

    const fgOn = () => O.fog.enabled,
      snOn = () => O.sun.enabled,
      lgOn = () => O.light.enabled;
    sw("atmos", "Fog", 0, "Enabled", "fog", "enabled");
    se(
      "atmos",
      "Fog",
      0,
      "Colour",
      "fog",
      "color",
      [
        [0, "Grey"],
        [1, "Blue"],
        [2, "Warm"],
        [3, "Green"],
      ],
      {
        en: fgOn,
      },
    );
    sl("atmos", "Fog", 0, "Brightness", "fog", "bright", 0.3, 1.5, 0.01, {
      en: fgOn,
    });
    sl("atmos", "Fog", 0, "Density", "fog", "amount", 0, 1, 0.01, {
      en: fgOn,
    });
    sl("atmos", "Fog", 0, "Visibility", "fog", "visibility", 1500, 40000, 500, {
      en: fgOn,
    });
    sl("atmos", "Fog", 0, "Start distance", "fog", "start", 0, 8000, 100, {
      en: fgOn,
    });
    sl("atmos", "Fog", 0, "Height falloff", "fog", "fall", 0, 1, 0.01, {
      en: fgOn,
    });
    sl("atmos", "Fog", 0, "Fog height", "fog", "base", -500, 3000, 50, {
      en: fgOn,
    });
    sl("atmos", "Fog", 0, "Sky fog", "fog", "sky", 0, 1, 0.01, {
      en: fgOn,
    });
    sw("atmos", "Sun", 1, "Enabled", "sun", "enabled");
    sl("atmos", "Sun", 1, "Rotate", "sun", "yaw", -180, 180, 1, {
      unit: " deg",
      en: snOn,
    });
    sl("atmos", "Sun", 1, "Height", "sun", "pitch", -45, 30, 1, {
      unit: " deg",
      en: snOn,
    });
    sl("atmos", "Sun", 1, "Light intensity", "sun", "intensity", 0, 2.5, 0.05, {
      en: snOn,
    });
    sl("atmos", "Sun", 1, "Ambient light", "sun", "ambient", 0, 2, 0.05, {
      en: snOn,
    });
    sl("atmos", "Sun", 1, "Warmth", "sun", "warmth", -1, 1, 0.05, {
      en: snOn,
    });
    sl("atmos", "Sun", 1, "Sun glow", "sun", "glow", 0, 2, 0.05, {
      en: snOn,
    });
    sl("atmos", "Sun", 1, "Light rays", "sun", "rays", 0, 1.5, 0.05, {
      en: snOn,
    });
    nt(
      "atmos",
      "Sun",
      1,
      "This moves the game's own sun: the lighting on objects and tanks and the real-time shadows follow it. Shadows baked into the map stay where they are.",
    );
    sw("atmos", "Lightning", 1, "Enabled", "light", "enabled");
    sw("atmos", "Lightning", 1, "Only while raining", "light", "onlyRain", {
      en: lgOn,
    });
    sl("atmos", "Lightning", 1, "Average gap", "light", "gap", 3, 60, 1, {
      unit: " s",
      en: lgOn,
    });
    sl(
      "atmos",
      "Lightning",
      1,
      "Flash strength",
      "light",
      "flash",
      0,
      2,
      0.05,
      {
        en: lgOn,
      },
    );
    sw("atmos", "Lightning", 1, "Bolts in the sky", "light", "bolts", {
      en: lgOn,
    });
    sw("atmos", "Lightning", 1, "Thunder sound", "light", "thunder", {
      en: lgOn,
    });
    sl("atmos", "Lightning", 1, "Thunder volume", "light", "vol", 0, 1, 0.01, {
      en: () => O.light.enabled && O.light.thunder,
    });
    C.push({
      tab: "atmos",
      sec: "Lightning",
      col: 1,
      label: "Test strike",
      t: "button",
      btn: "Strike",
      fn: () => {
        if (O.light.enabled) O.light.now = 1;
        else toast("Turn lightning on first");
      },
    });

    // Skybox changer: the thumbnails come from the skies the game has cached, picking one swaps the sky the game draws (see SKYBOX CHANGER)
    C.push({
      tab: "sky",
      sec: "Skybox changer",
      col: 0,
      label: "Random sky",
      t: "button",
      btn: "Shuffle",
      fn: () => {
        SKYLIB.avail().then((list) => {
          if (!list.length) {
            toast("No skyboxes in the game cache yet");
            return;
          }
          const pool = list.filter((x) => x.key !== O.sky.set),
            x = pool[Math.floor(Math.random() * pool.length)] || list[0];
          set(skyCtl, x.key);
          changed();
          toast("Sky: " + x.name);
        });
      },
    });
    C.push({
      tab: "sky",
      sec: "Skybox changer",
      col: 0,
      label: "Custom sky",
      t: "button",
      btn: "Add...",
      fn: () => {
        const inp = h("input", {
          type: "file",
          accept: "image/*",
          multiple: "",
        });
        inp.style.display = "none";
        inp.addEventListener("change", async () => {
          const files = Array.from(inp.files || []);
          inp.remove();
          if (!files.length) return;
          toast("Processing...");
          try {
            const r = await SKYLIB.add(files);
            set(skyCtl, r.key);
            changed();
            render();
            toast('Added "' + r.name + '"');
          } catch (e) {
            toast(String((e && e.message) || e));
          }
        });
        (document.body || document.documentElement).append(inp);
        inp.click();
      },
    });
    nt(
      "sky",
      "Skybox changer",
      0,
      "Custom skies: pick one 2:1 panorama picture (equirectangular), or six face pictures named like front / back / left / right / up / down (also ft bk lf rt up dn, or px nx py ny pz nz). Six faces with other names are lined up by their edges. The pictures are stored in this browser only; the cross on a custom tile removes it.",
    );
    nt(
      "sky",
      "Skybox changer",
      0,
      'Replaces the sky drawn behind the map with any skybox the game has stored on this computer. It only changes what you see: the map, the lighting and other players are not affected. Weather, fog and the other effects work on top of it. Choose "Map default" to get the sky of the map back.',
    );
    const skyCtl = {
      tab: "sky",
      sec: "Skies",
      col: 0,
      label: "Skybox",
      t: "skygrid",
      o: "sky",
      k: "set",
    };
    C.push(skyCtl);

    if (animGroups.length) {
      se(
        "anim",
        "Skin animations",
        0,
        "Skin",
        "anim",
        "activeGroup",
        animGroups.map((g) => [g, animMeta[g].title]),
        {
          after: () => {
            animSync();
            render();
          },
        },
      );
      C.push({
        tab: "anim",
        sec: "Skin animations",
        col: 0,
        label: "Detection",
        t: "status",
        fn: () => animations.getStatus(O.anim.activeGroup),
      });
      se(
        "anim",
        "Skin animations",
        0,
        "Animation preset",
        "anim",
        "activePreset",
        () => {
          const m = animMeta[O.anim.activeGroup];
          return m
            ? Object.entries(m.presets).map(([k, p]) => [k, p.label])
            : [];
        },
        {
          vol: true,
          after: () => {
            animSync();
            refresh();
          },
        },
      );
      C.push({
        tab: "anim",
        sec: "Skin animations",
        col: 0,
        t: "dynnote",
        fn: () => {
          const m = animMeta[O.anim.activeGroup];
          if (!m) return "";
          const p =
            m.presets[O.anim.activePreset] || m.presets[m.defaultPreset];
          let txt = p ? p.desc : "";
          if (m.isHull)
            txt +=
              O.anim.hullTrigger === "always"
                ? ". Hull animations run independently of turret trigger."
                : O.anim.hullTrigger === "shoot"
                  ? ". Animating on shoot."
                  : ". Hull animations turned off.";
          return txt;
        },
      });
      for (const g of animGroups) {
        C.push({
          tab: "anim",
          sec: "Hidden",
          col: 0,
          t: "hidden",
          o: "anim",
          k: "preset_" + g,
          opts: Object.keys(animMeta[g].presets).map((k) => [k, k]),
          after: () => animSync(),
        });
      }
      se(
        "anim",
        "Playback",
        1,
        "Turret trigger",
        "anim",
        "trigger",
        [
          ["shoot", "On Shoot"],
          ["always", "Always (Preview)"],
        ],
        {
          after: () => {
            animSync();
            refresh();
          },
        },
      );
      se(
        "anim",
        "Playback",
        1,
        "Hull trigger",
        "anim",
        "hullTrigger",
        [
          ["always", "Always"],
          ["shoot", "On Shoot"],
          ["off", "Off"],
        ],
        {
          after: () => {
            animSync();
            refresh();
          },
        },
      );
      sl("anim", "Playback", 1, "Speed", "anim", "speed", 0.4, 2.5, 0.05, {
        unit: "x",
        after: () => animSync(),
      });
      sl(
        "anim",
        "Playback",
        1,
        "Intensity",
        "anim",
        "intensity",
        0.4,
        1.8,
        0.05,
        {
          unit: "x",
          after: () => animSync(),
        },
      );
      nt(
        "anim",
        "Playback",
        1,
        "Skin animation presets and playback settings are saved per skin and included in ProTanki Pack JSON presets.",
      );
    } else {
      nt(
        "anim",
        "Skin Animations Unavailable",
        0,
        "Running standalone without TextureSwapper cache patch — the Skin Animations module is only bundled automatically when applied via TextureSwapper (or loaded separately).",
      );
    }

    sw("hud", "Supplies", 0, "Center at the bottom", "hud", "center", {
      after: () => hudApply(),
    });
    sl("hud", "Supplies", 0, "Scale", "hud", "scale", 0.5, 3, 0.05, {
      unit: "\u00d7",
      after: () => hudApply(),
    });
    sw("hud", "Supplies", 0, "Classic tank icons", "hud", "classicIcons");
    nt(
      "hud",
      "Supplies",
      0,
      "Moves the supplies bar from the bottom left corner to the middle of the screen. Classic tank icons replaces the supply icons below tank health bars independently of which supply bar you use (takes effect on next load if already cached).",
    );
    sw("hud", "Classic supply bar", 1, "Enabled", "hud", "classic", {
      after: () => hudApply(),
    });
    sw("hud", "Classic supply bar", 1, "Ready beam", "hud", "beam", {
      en: () => O.hud.classic,
    });
    sl(
      "hud",
      "Classic supply bar",
      1,
      "Effect duration",
      "hud",
      "effectTime",
      0,
      120,
      1,
      {
        unit: " s",
        en: () => O.hud.classic,
      },
    );
    nt(
      "hud",
      "Classic supply bar",
      1,
      "Draws the supplies bar in its older look: pixel art slots, flashes when a supply is used or ready again, grey count while recharging. It follows the position and size set on the left and only changes how the bar looks, not how supplies are used.",
    );
    C.push({
      tab: "general",
      sec: "Menu",
      col: 0,
      label: "Reset all settings",
      t: "button",
      btn: "Reset",
      fn: () => {
        applyDict(FACTORY);
        setPresetName("Default");
        toast("Settings reset");
      },
    });
    C.push({
      tab: "general",
      sec: "Info",
      col: 1,
      t: "note",
      text: "Use the Keybinds tab to bind custom hotkeys for any feature, and the Presets tab to save, delete, import, or share JSON presets.",
    });
    C.push({
      tab: "general",
      sec: "Info",
      col: 1,
      t: "note",
      text: "All settings, keybinds, presets, and window position are saved automatically in this browser.",
    });

    const DEFAULT_KEYBINDS = {
      precipCycle: "Alt+KeyP",
      snowg: "",
      wet: "",
      bloom: "Alt+KeyB",
      cam: "Alt+KeyM",
      dof: "",
      grade: "",
      sharp: "",
      fog: "",
      sun: "",
      light: "",
      strike: "",
      hudCenter: "",
      hudClassic: "",
      animTrigger: "",
    };
    let keybinds = Object.assign(
      {},
      DEFAULT_KEYBINDS,
      ls.get(LSK.keybinds, {}) || {},
    );
    const kb = (sec, col, label, bindId) =>
      C.push({
        tab: "keybinds",
        sec,
        col,
        label,
        t: "keybind",
        bindId,
      });
    const HOST_HOTKEY_RAW = "__TS_SHADER_HOTKEY__";
    const isStandaloneBuild = HOST_HOTKEY_RAW.startsWith("__TS_");
    C.push({
      tab: "keybinds",
      sec: "Menu",
      col: 0,
      label: "Toggle ProTanki Pack menu",
      t: "keybind",
      bindId: "menu",
      disabled: isStandaloneBuild,
      disabledReason: "Menu hotkey sync requires TextureSwapper",
    });
    if (isStandaloneBuild) {
      nt(
        "keybinds",
        "Menu",
        0,
        "Running standalone (browser/userscript): menu hotkey rebinding is disabled because TextureSwapper is not attached.",
      );
    }
    C.push({
      tab: "keybinds",
      sec: "Menu",
      col: 0,
      label: "Reset all keybinds",
      t: "button",
      btn: "Reset",
      fn: () => {
        keybinds = Object.assign({}, DEFAULT_KEYBINDS);
        ls.set(LSK.keybinds, keybinds);
        menuKey = HOST_HOTKEY;
        ls.set(LSK.hostKey, HOST_HOTKEY);
        ls.set(LSK.key, menuKey);
        syncMenuHotkeyToHost(menuKey);
        refresh();
        toast("Keybinds reset");
      },
    });
    kb("Weather", 0, "Cycle precipitation (Off / Rain / Snow)", "precipCycle");
    kb("Weather", 0, "Toggle snow ground", "snowg");
    kb("Weather", 0, "Toggle wet ground", "wet");
    kb("Camera", 0, "Toggle bloom", "bloom");
    kb("Camera", 0, "Toggle motion blur", "cam");
    kb("Camera", 0, "Toggle depth of field", "dof");
    kb("Color", 1, "Toggle color grading", "grade");
    kb("Color", 1, "Toggle sharpening", "sharp");
    kb("Atmosphere", 1, "Toggle fog", "fog");
    kb("Atmosphere", 1, "Toggle sun", "sun");
    kb("Atmosphere", 1, "Toggle lightning", "light");
    kb("Atmosphere", 1, "Trigger lightning strike", "strike");
    kb("HUD & Animations", 1, "Toggle centered supplies", "hudCenter");
    kb("HUD & Animations", 1, "Toggle classic supply bar", "hudClassic");
    if (animGroups.length)
      kb("HUD & Animations", 1, "Cycle animation trigger mode", "animTrigger");
    nt(
      "keybinds",
      "Tips",
      0,
      "Click a button and press any key or combo (e.g. Alt+B, F7). Press Backspace or Delete to unbind, or Escape to cancel.",
    );

    // ---------- values, persistence, presets ----------
    const keyOf = (c) => c.o + "." + c.k;
    const persisted = C.filter((c) => c.o && !c.vol);
    const get = (c) => O[c.o][c.k];
    const set = (c, v) => {
      O[c.o][c.k] = v;
      if (c.after) c.after(v);
    };
    const snapshot = () => {
      const d = {};
      for (const c of persisted) d[keyOf(c)] = get(c);
      return d;
    };
    let UPD = [];

    function refresh() {
      for (const f of UPD) f();
    }
    const FACTORY = snapshot();
    const applyDict = (d) => {
      for (const c of persisted) {
        const k = keyOf(c);
        if (!(k in d)) continue;
        let v = d[k];
        if (c.t === "toggle") v = !!v;
        else if (c.t === "slider") {
          v = Number(v);
          if (!isFinite(v)) continue;
          v = clamp(v, c.min, c.max);
        } else if (c.t === "select" || c.t === "hidden") {
          const opts = typeof c.opts === "function" ? c.opts() : c.opts;
          if (!opts.some((o) => o[0] === v)) continue;
        } else if (c.t === "skygrid") {
          v = String(v);
          if (v && v.slice(0, 2) !== "c:" && !SKYLIB.byKey(v)) continue;
        }
        set(c, v);
      }
      persist();
      refresh();
    };
    let saveTimer = 0;
    const persist = () => {
      clearTimeout(saveTimer);
      saveTimer = setTimeout(() => ls.set(LSK.settings, snapshot()), 250);
    };
    const loadedSettings = ls.get(LSK.settings, null);
    if (loadedSettings) applyDict(loadedSettings);
    if (
      animations &&
      (!loadedSettings ||
        !Object.keys(loadedSettings).some((k) => k.startsWith("anim.")))
    ) {
      try {
        const ac = animations.getConfig();
        const mig = {
          "anim.activeGroup": ac.activeGroup,
          "anim.trigger": ac.trigger,
          "anim.hullTrigger": ac.hullTrigger || "always",
          "anim.speed": ac.speed,
          "anim.intensity": ac.intensity,
        };
        for (const g of animGroups) {
          if (ac.presets && ac.presets[g])
            mig["anim.preset_" + g] = ac.presets[g];
        }
        applyDict(mig);
      } catch (e) {}
    }
    // earlier v1.15 builds shipped other snow / splash defaults; move anyone still on those to the current ones
    try {
      if (loadedSettings) {
        const sg = O.snowg,
          wt = O.wet;
        if (sg.trackDepth === 0.85) sg.trackDepth = 0.55;
        if (sg.build === 40) sg.build = 360;
        if (sg.fade === 240) sg.fade = 90;
        if (sg.melt === 25) sg.melt = 90;
        if (sg.contact === 60 || sg.contact === 90) sg.contact = 38;
        if (wt.contact === 60 || wt.contact === 90) wt.contact = 38;
      }
    } catch (e) {}
    const QUALITY_TIERS = {
      low: {
        label: "Low",
        precipScale: 0.25,
        camSamples: 6,
        dofEnabled: false,
        dofSamples: 8,
        wetSplash: false,
        wetDist: 4500,
        snowTracks: false,
        snowRange: 6000,
        raysScale: 0,
      },
      medium: {
        label: "Medium",
        precipScale: 0.55,
        camSamples: 12,
        dofEnabled: null,
        dofSamples: 14,
        wetSplash: true,
        wetDist: 7000,
        snowTracks: true,
        snowRange: 10000,
        raysScale: 0.5,
      },
      high: {
        label: "High",
        precipScale: 1.0,
        camSamples: 16,
        dofEnabled: null,
        dofSamples: 24,
        wetSplash: true,
        wetDist: 9000,
        snowTracks: true,
        snowRange: 14000,
        raysScale: 1.0,
      },
      veryhigh: {
        label: "Very High",
        precipScale: 1.6,
        camSamples: 32,
        dofEnabled: null,
        dofSamples: 36,
        wetSplash: true,
        wetDist: 15000,
        snowTracks: true,
        snowRange: 22000,
        raysScale: 1.25,
      },
    };
    const VISUAL_FACTORY = Object.fromEntries(
      Object.entries(FACTORY).filter(
        ([k]) => !k.startsWith("anim.") && !k.startsWith("hud."),
      ),
    );
    const BUILTIN_PRESETS = [
      {
        name: "Clear & Crisp",
        desc: "Clean daylight look with subtle bloom, sharpening, and vibrant colors.",
        settings: Object.assign({}, VISUAL_FACTORY, {
          "precip.type": "off",
          "wet.enabled": false,
          "snowg.enabled": false,
          "bloom.enabled": true,
          "bloom.threshold": 0.55,
          "bloom.intensity": 0.85,
          "cam.enabled": false,
          "dof.enabled": false,
          "grade.enabled": true,
          "grade.contrast": 1.08,
          "grade.sat": 1.12,
          "grade.expo": 0.05,
          "sharp.enabled": true,
          "sharp.amount": 0.65,
          "fog.enabled": false,
          "sun.enabled": false,
        }),
      },
      {
        name: "Stormy Rain",
        desc: "Heavy rainfall, wet puddles with tank splashes, lightning, and cool moody fog.",
        settings: Object.assign({}, VISUAL_FACTORY, {
          "precip.type": "rain",
          "precip.count": 12000,
          "precip.alpha": 0.55,
          "precip.mood": 0.45,
          "wet.enabled": true,
          "wet.wet": 0.85,
          "wet.puddle": 0.8,
          "wet.refl": 1.0,
          "wet.splash": true,
          "snowg.enabled": false,
          "bloom.enabled": true,
          "bloom.intensity": 1.1,
          "fog.enabled": true,
          "fog.color": 1,
          "fog.amount": 0.55,
          "fog.visibility": 14000,
          "light.enabled": true,
          "light.onlyRain": true,
          "light.bolts": true,
          "light.thunder": true,
          "grade.enabled": true,
          "grade.temp": -0.15,
          "grade.sat": 0.92,
        }),
      },
      {
        name: "Winter Blizzard",
        desc: "Snowfall with ground buildup, tank tracks, soft bloom, and crisp winter tones.",
        settings: Object.assign({}, VISUAL_FACTORY, {
          "precip.type": "snow",
          "precip.count": 10000,
          "precip.alpha": 0.65,
          "precip.mood": 0.3,
          "wet.enabled": false,
          "snowg.enabled": true,
          "snowg.cover": 0.95,
          "snowg.tracks": true,
          "bloom.enabled": true,
          "bloom.threshold": 0.5,
          "bloom.intensity": 1.0,
          "fog.enabled": true,
          "fog.color": 0,
          "fog.amount": 0.45,
          "fog.visibility": 15000,
          "grade.enabled": true,
          "grade.temp": -0.2,
          "grade.contrast": 1.05,
        }),
      },
      {
        name: "Golden Sunset",
        desc: "Warm low-angle sun with glow, light rays, and golden color grading.",
        settings: Object.assign({}, VISUAL_FACTORY, {
          "precip.type": "off",
          "wet.enabled": false,
          "snowg.enabled": false,
          "bloom.enabled": true,
          "bloom.threshold": 0.4,
          "bloom.intensity": 1.35,
          "sun.enabled": true,
          "sun.yaw": 35,
          "sun.pitch": -12,
          "sun.intensity": 1.35,
          "sun.warmth": 0.55,
          "sun.glow": 1.1,
          "sun.rays": 0.75,
          "grade.enabled": true,
          "grade.temp": 0.25,
          "grade.sat": 1.15,
          "grade.vig": 0.25,
          "sharp.enabled": true,
          "sharp.amount": 0.4,
        }),
      },
      {
        name: "Cinematic",
        desc: "Filmic depth of field, motion blur, bloom, vignette, and rich contrast.",
        settings: Object.assign({}, VISUAL_FACTORY, {
          "precip.type": "off",
          "wet.enabled": false,
          "snowg.enabled": false,
          "bloom.enabled": true,
          "bloom.threshold": 0.42,
          "bloom.intensity": 1.25,
          "cam.enabled": true,
          "cam.strength": 0.65,
          "dof.enabled": true,
          "dof.auto": true,
          "dof.strength": 1.0,
          "grade.enabled": true,
          "grade.contrast": 1.12,
          "grade.sat": 1.08,
          "grade.vig": 0.35,
          "sharp.enabled": true,
          "sharp.amount": 0.5,
        }),
      },
      {
        name: "Classic 2014",
        desc: "Clean sharp visuals with subtle bloom and no weather effects.",
        settings: Object.assign({}, VISUAL_FACTORY, {
          "precip.type": "off",
          "wet.enabled": false,
          "snowg.enabled": false,
          "bloom.enabled": true,
          "bloom.threshold": 0.6,
          "bloom.intensity": 0.75,
          "cam.enabled": false,
          "dof.enabled": false,
          "sharp.enabled": true,
          "sharp.amount": 0.5,
        }),
      },
    ];
    let qualityTier = ls.get(LSK.quality, "high");
    if (!QUALITY_TIERS[qualityTier]) qualityTier = "high";
    const scaleDictByQuality = (baseDict, tierKey) => {
      const t = QUALITY_TIERS[tierKey] || QUALITY_TIERS.high;
      const d = Object.assign({}, baseDict);
      if (typeof d["precip.count"] === "number") {
        const rawCount = d["precip.count"];
        d["precip.count"] =
          rawCount <= 0
            ? 0
            : clamp(
                Math.round((rawCount * t.precipScale) / 250) * 250,
                1000,
                30000,
              );
      }
      if ("cam.samples" in d) d["cam.samples"] = t.camSamples;
      if (t.dofEnabled === false) d["dof.enabled"] = false;
      if ("dof.samples" in d) d["dof.samples"] = t.dofSamples;
      if ("wet.splash" in d && d["wet.splash"]) d["wet.splash"] = t.wetSplash;
      if ("wet.maxDist" in d) d["wet.maxDist"] = t.wetDist;
      if ("snowg.tracks" in d && d["snowg.tracks"])
        d["snowg.tracks"] = t.snowTracks;
      if ("snowg.range" in d) d["snowg.range"] = t.snowRange;
      if (typeof d["sun.rays"] === "number" && d["sun.rays"] > 0)
        d["sun.rays"] = +clamp(d["sun.rays"] * t.raysScale, 0, 1.5).toFixed(2);
      return d;
    };
    let presets = ls.get(LSK.presets, {});
    let presetName = ls.get(LSK.preset, "Default");
    if (
      presetName !== "Default" &&
      !BUILTIN_PRESETS.some((p) => p.name === presetName) &&
      !presets[presetName]
    )
      presetName = "Default";
    const HOST_HOTKEY = isStandaloneBuild
      ? "Insert"
      : HOST_HOTKEY_RAW;
    const prevHostKey = ls.get(LSK.hostKey, null);
    let menuKey = ls.get(LSK.key, HOST_HOTKEY);
    if (prevHostKey !== HOST_HOTKEY) {
      menuKey = HOST_HOTKEY;
      ls.set(LSK.hostKey, HOST_HOTKEY);
      ls.set(LSK.key, menuKey);
    }
    if (isStandaloneBuild && !animations) {
      console.warn(
        "[ProTanki Pack] Running standalone (browser/userscript) without TextureSwapper cache patch — Skin Animations module is not bundled unless loaded separately.",
      );
    }
    let warnedNoSwapper = false;
    const warnNoSwapper = (fromHotkeyChange) => {
      if (!warnedNoSwapper) {
        warnedNoSwapper = true;
        console.warn(
          "[ProTanki Pack] TextureSwapper is not detected on port 47921; menu hotkey changes are saved locally in this browser and will not sync to TextureSwapper's Settings window.",
        );
      }
      if (fromHotkeyChange && typeof notifyInGame === "function") {
        notifyInGame(
          "Menu key saved locally (TextureSwapper not detected — won't sync to app)",
          true,
        );
      }
    };
    const syncMenuHotkeyToHost = (key, notifyOnOffline) => {
      if (!key) return;
      if (!HOST_IPC_TOKEN) {
        warnNoSwapper(notifyOnOffline);
        return;
      }
      try {
        fetch("http://127.0.0.1:47921/ptpack-hotkey", {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            "X-PTPack-Token": HOST_IPC_TOKEN,
          },
          body: JSON.stringify({ hotkey: key }),
        })
          .then((r) => {
            if (!r || !r.ok) warnNoSwapper(notifyOnOffline);
          })
          .catch(() => warnNoSwapper(notifyOnOffline));
      } catch (e) {
        warnNoSwapper(notifyOnOffline);
      }
    };
    const GAME_DEFAULT_CONTROLS = {
      turretLeft: { label: "Turret Left", keys: ["KeyZ", "Comma"] },
      turretRight: { label: "Turret Right", keys: ["KeyX", "Period"] },
      centerTurret: { label: "Center Turret", keys: ["KeyC", "Slash"] },
      shoot: { label: "Shoot", keys: ["Space"] },
      turnLeft: { label: "Turn Left", keys: ["ArrowLeft", "KeyA"] },
      forward: { label: "Move Forward", keys: ["ArrowUp", "KeyW"] },
      turnRight: { label: "Turn Right", keys: ["ArrowRight", "KeyD"] },
      back: { label: "Move Backward", keys: ["ArrowDown", "KeyS"] },
      supply1: { label: "First Aid", keys: ["Digit1", "Numpad1"] },
      supply2: { label: "Double Armor", keys: ["Digit2", "Numpad2"] },
      supply3: { label: "Double Damage", keys: ["Digit3", "Numpad3"] },
      supply4: { label: "Speed Boost", keys: ["Digit4", "Numpad4"] },
      supply5: { label: "Mine", keys: ["Digit5", "Numpad5"] },
      supply6: { label: "Gold Box", keys: ["Digit6", "Numpad6"] },
      selfDestruct: { label: "Self-Destruct", keys: ["Delete", "Ctrl+Backslash"] },
      dropFlag: { label: "Drop Flag", keys: ["KeyF"] },
      pause: { label: "Pause", keys: ["KeyP"] },
      viewIncrease: { label: "Screen Size +", keys: ["NumpadAdd", "Ctrl+Equal"] },
      viewDecrease: { label: "Screen Size -", keys: ["NumpadSubtract", "Ctrl+Minus"] },
      fullscreen: { label: "Fullscreen", keys: ["F11"] },
      openGarage: { label: "Open Garage", keys: ["KeyG"] },
      cameraUp: { label: "Camera Up", keys: ["PageUp", "BracketLeft", "KeyQ"] },
      cameraDown: { label: "Camera Down", keys: ["PageDown", "BracketRight", "KeyE"] },
      lookBack: { label: "Look Back", keys: [] },
    };
    const getGameHotkeys = () => {
      let saved = null;
      try {
        const sRaw = sessionStorage.getItem("pt.settings.session");
        const lRaw = localStorage.getItem("pt.settings");
        const sObj = sRaw ? JSON.parse(sRaw) : null;
        const lObj = lRaw ? JSON.parse(lRaw) : null;
        saved =
          (sObj && typeof sObj.keybinds === "object" && sObj.keybinds) ||
          (lObj && typeof lObj.keybinds === "object" && lObj.keybinds) ||
          null;
      } catch (e) {}

      const usedByCustom = new Set();
      if (saved) {
        for (const action in GAME_DEFAULT_CONTROLS) {
          const arr = saved[action];
          if (Array.isArray(arr)) {
            for (const k of arr) {
              if (typeof k === "string" && k) usedByCustom.add(k);
            }
          }
        }
      }

      const map = new Map();
      for (const action in GAME_DEFAULT_CONTROLS) {
        const def = GAME_DEFAULT_CONTROLS[action];
        const customArr = saved && Array.isArray(saved[action]) ? saved[action] : null;
        const keys = customArr
          ? customArr.filter((k) => typeof k === "string" && k).slice(0, 3)
          : def.keys.filter((k) => k && !usedByCustom.has(k));
        for (const k of keys) {
          if (!map.has(k)) map.set(k, def.label);
        }
      }
      return map;
    };
    const findKeyConflict = (bindStr, excludeBindId) => {
      if (!bindStr) return null;
      if (excludeBindId !== "menu" && bindStr === menuKey) {
        return "Toggle ProTanki Pack menu";
      }
      for (const id in keybinds) {
        if (id === excludeBindId) continue;
        if (keybinds[id] === bindStr) {
          const ctrl = C.find((c) => c.t === "keybind" && c.bindId === id);
          return (ctrl && ctrl.label) || id;
        }
      }
      const gameHotkeys = getGameHotkeys();
      if (gameHotkeys.has(bindStr)) {
        return "In-game (" + gameHotkeys.get(bindStr) + ")";
      }
      return null;
    };
    let notifyInGame = null;
    const reconcilePackKeybindsWithGame = (notify) => {
      const gameHotkeys = getGameHotkeys();
      const seen = new Set(gameHotkeys.keys());
      let changedMenu = false;
      let changedBinds = false;
      const notices = [];

      if (seen.has(menuKey)) {
        const oldMenuKey = menuKey;
        const fallbacks = [
          "Insert",
          "F8",
          "F7",
          "F9",
          "F10",
          "Home",
          "End",
          "Backquote",
          "F6",
          "F12",
          "F4",
          "F3",
          "F2",
          "F1",
          "Pause",
          "ScrollLock",
        ];
        const usedByPack = new Set(Object.values(keybinds).filter(Boolean));
        const replacement = fallbacks.find((k) => !seen.has(k) && !usedByPack.has(k));
        if (replacement) {
          menuKey = replacement;
          ls.set(LSK.key, menuKey);
          ls.set(LSK.hostKey, menuKey);
          syncMenuHotkeyToHost(menuKey);
          changedMenu = true;
          notices.push(
            "ProTanki Pack menu key changed to " +
              keyName(menuKey) +
              " (" +
              keyName(oldMenuKey) +
              " is used in-game)",
          );
        }
      }
      seen.add(menuKey);

      for (const id in keybinds) {
        const b = keybinds[id];
        if (!b) continue;
        if (seen.has(b)) {
          keybinds[id] = "";
          changedBinds = true;
          if (gameHotkeys.has(b)) {
            const ctrl = C.find((c) => c.t === "keybind" && c.bindId === id);
            notices.push(
              "Cleared pack keybind '" +
                ((ctrl && ctrl.label) || id) +
                "' (" +
                b +
                " is used in-game)",
            );
          }
        } else {
          seen.add(b);
        }
      }

      if (changedBinds) ls.set(LSK.keybinds, keybinds);
      if ((changedMenu || changedBinds) && typeof refresh === "function") {
        refresh();
      }
      if (notify && notices.length && typeof notifyInGame === "function") {
        notifyInGame(notices.join(" • "), true);
      }
    };
    if (!Storage.prototype.__ptpackWrapped) {
      const origSetItem = Storage.prototype.setItem;
      Storage.prototype.setItem = function (k, v) {
        origSetItem.apply(this, arguments);
        if (
          (this === localStorage && k === "pt.settings") ||
          (this === sessionStorage && k === "pt.settings.session")
        ) {
          try {
            reconcilePackKeybindsWithGame(true);
          } catch (e) {}
        }
      };
      Storage.prototype.__ptpackWrapped = true;
    }
    const keyName = (code) =>
      String(code)
        .replace(/^Key/, "")
        .replace(/^Digit/, "")
        .replace(/^Numpad/, "Num ");

    // ---------- DOM ----------
    const h = (tag, props, ...kids) => {
      const el = document.createElement(tag);
      if (props)
        for (const k in props) {
          if (k === "class") el.className = props[k];
          else if (k === "html") el.innerHTML = props[k];
          else el.setAttribute(k, props[k]);
        }
      for (const kid of kids) if (kid != null) el.append(kid);
      return el;
    };
    const ICON = {
      cloud:
        '<path d="M7 15a4 4 0 0 1-.4-7.98A5.5 5.5 0 0 1 17.2 7.4 3.8 3.8 0 0 1 17 15"/><path d="M9 18l-1 3M13 18l-1 3M17 18l-1 3"/>',
      drop: '<path d="M12 3s6.5 6.4 6.5 11a6.5 6.5 0 0 1-13 0C5.5 9.4 12 3 12 3z"/><path d="M9 14.5a3 3 0 0 0 2.5 2.9"/>',
      sun: '<circle cx="12" cy="12" r="4"/><path d="M12 3v2M12 19v2M3 12h2M19 12h2M5.6 5.6L7 7M17 17l1.4 1.4M5.6 18.4L7 17M17 7l1.4-1.4"/>',
      blur: '<path d="M3 8h7M3 12h11M3 16h7"/><circle cx="17" cy="12" r="4"/>',
      palette:
        '<path d="M12 3a9 9 0 1 0 0 18c1.2 0 1.8-.8 1.5-1.8-.3-1 .3-2 1.4-2H17a4 4 0 0 0 4-4c0-5-4-10-9-10z"/><circle cx="8" cy="11" r="1"/><circle cx="12" cy="7.5" r="1"/><circle cx="16" cy="11" r="1"/>',
      bolt: '<path d="M13 3L5 13.5h6L10 21l8-10.5h-6z"/>',
      sky: '<path d="M3 18h18"/><path d="M7.5 18a4.5 4.5 0 0 1 9 0"/><path d="M12 5v3M4.6 9.6l2 2M19.4 9.6l-2 2"/>',
      sliders:
        '<path d="M4 7h9M19 7h1M4 17h1M11 17h9"/><circle cx="16" cy="7" r="2.2"/><circle cx="8" cy="17" r="2.2"/>',
      search: '<circle cx="11" cy="11" r="6"/><path d="M20 20l-4.2-4.2"/>',
      save: '<path d="M5 4h11l3 3v13H5z"/><path d="M8 4v5h7V4M8 20v-6h8v6"/>',
      hud: '<rect x="3" y="4" width="18" height="16" rx="2"/><path d="M8 16h8"/>',
      anim: '<path d="M12 3l2.2 4.8L19 10l-4.8 2.2L12 17l-2.2-4.8L5 10l4.8-2.2z"/>',
      bookmark: '<path d="M6 4h12v17l-6-4-6 4z"/>',
      key: '<circle cx="8" cy="15" r="4"/><path d="M11 12l8-8M16 7l3 3"/>',
      share:
        '<path d="M4 12v8a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-8"/><polyline points="16 6 12 2 8 6"/><line x1="12" y1="2" x2="12" y2="15"/>',
      upload:
        '<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/>',
      chev: '<path d="M6 9l6 6 6-6"/>',
      trash: '<path d="M5 7h14M10 7V4h4v3M7 7l1 13h8l1-13M10 11v6M14 11v6"/>',
      logs: '<path d="M14 3H6a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V9z"/><path d="M14 3v6h6M8 13h8M8 17h5"/>',
      x: '<path d="M6 6l12 12M18 6L6 18"/>',
    };
    const icon = (n, size) =>
      h("span", {
        class: "ico",
        html:
          '<svg viewBox="0 0 24 24" width="' +
          (size || 18) +
          '" height="' +
          (size || 18) +
          '" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">' +
          ICON[n] +
          "</svg>",
      });

    const CSS = ":host{all:initial}\n*{box-sizing:border-box;margin:0;padding:0}\n.win{--bg:#14151b;--side:#101116;--card:#181920;--line:#22242d;--line2:#2b2d38;--txt:#e9eaf0;--mut:#7f828e;--acc:#4f7dff;\n position:fixed;left:50%;top:50%;width:min(960px,95vw);height:min(680px,92vh);display:none;flex-direction:row;\n transform:translate(calc(-50% + var(--dx,0px)),calc(-50% + var(--dy,0px))) scale(.97);opacity:0;\n background:var(--bg);color:var(--txt);border:1px solid var(--line);border-radius:18px;overflow:hidden;\n font:13px/1.25 'Noto Sans','Inter',system-ui,-apple-system,'Segoe UI',sans-serif;box-shadow:0 30px 90px rgba(0,0,0,.65),0 0 0 1px rgba(255,255,255,.02) inset;\n pointer-events:auto;user-select:none;-webkit-user-select:none;transition:opacity .14s ease,transform .14s ease}\n.win.show{display:flex}\n.win.open{opacity:1;transform:translate(calc(-50% + var(--dx,0px)),calc(-50% + var(--dy,0px))) scale(1)}\n.ico{display:inline-flex;flex:none}\n.side{width:184px;flex:none;background:var(--side);border-right:1px solid var(--line);display:flex;flex-direction:column;padding:16px 12px 12px}\n.brand{display:flex;align-items:center;gap:10px;padding:0 6px 18px}\n.logo{width:38px;height:38px;border-radius:10px;background:linear-gradient(135deg,#5b86ff,#3a5fe0);display:grid;place-items:center;font-weight:800;font-size:14px;letter-spacing:.5px;color:#fff;flex:none;box-shadow:0 4px 14px rgba(79,125,255,.35)}\n.bname{font-weight:700;font-size:14px}.bsub{font-size:10px;color:var(--mut);margin-top:3px}\n.grp{font-size:10.5px;letter-spacing:.9px;text-transform:uppercase;color:#5f626e;padding:12px 8px 8px;font-weight:600}\n.nav{display:flex;align-items:center;gap:11px;height:40px;padding:0 12px;border-radius:10px;color:#a4a7b3;cursor:pointer;margin-bottom:2px;transition:background .12s,color .12s}\n.nav:hover{background:#171820;color:#d6d8e0}\n.nav.on{background:#1e202a;color:#fff}.nav.on .ico{color:var(--acc)}\n.nav .txt{font-size:13.5px;font-weight:500}\n.sp{flex:1}\n.main{flex:1;min-width:0;display:flex;flex-direction:column;position:relative}\n.top{height:58px;flex:none;display:flex;align-items:center;gap:10px;padding:0 14px;border-bottom:1px solid var(--line);cursor:grab}\n.top:active{cursor:grabbing}\n.pill{height:34px;display:flex;align-items:center;border:1px solid var(--line2);background:#15161d;border-radius:10px;color:var(--txt)}\n.pbtn{height:100%;display:flex;align-items:center;gap:8px;padding:0 11px;cursor:pointer;color:#c5c8d3;border-radius:9px}\n.pbtn:hover{color:#fff;background:#1b1c25}\n.pbtn.sep{border-left:1px solid var(--line2);border-radius:0 9px 9px 0}\n.pbtn.dis{opacity:.4;cursor:default}\n.pname{min-width:92px;justify-content:space-between;font-size:13.5px}\n.pin{height:100%;width:170px;background:transparent;border:0;outline:0;color:var(--txt);font:inherit;padding:0 12px;user-select:text}\n.srch{display:flex;align-items:center;height:34px;border-radius:10px;color:#aeb1bd}\n.srch input{width:0;opacity:0;border:0;outline:0;background:transparent;color:var(--txt);font:inherit;transition:width .16s,opacity .16s;user-select:text}\n.srch.on{border:1px solid var(--line2);background:#15161d;padding-left:4px}\n.srch.on input{width:170px;opacity:1;padding:0 6px}\n.srch .pbtn{height:32px;padding:0 8px}\n.content{flex:1;overflow:auto;padding:14px 16px 18px;display:grid;grid-template-columns:1fr 1fr;gap:16px;align-content:start;scrollbar-width:thin;scrollbar-color:#2b2d38 transparent}\n.col{display:flex;flex-direction:column;gap:18px;min-width:0}\n.sec h4{font-size:10.5px;letter-spacing:.9px;text-transform:uppercase;color:#6a6d79;font-weight:600;margin:0 4px 8px}\n.card{background:var(--card);border:1px solid var(--line);border-radius:14px;padding:0 14px}\n.row{min-height:40px;padding:6px 0;display:flex;align-items:center;justify-content:space-between;gap:10px;border-bottom:1px solid #20222b;transition:opacity .15s}\n.row:last-child{border-bottom:0}\n.row.dim{opacity:.38;pointer-events:none}\n.lab{color:#dcdee6;font-size:12.5px;line-height:1.25;flex:1;min-width:0;white-space:normal;word-break:break-word}\n.ctl{display:flex;align-items:center;flex:none}\n.note{padding:12px 0;color:var(--mut);font-size:12px;line-height:1.5;border-bottom:1px solid #20222b}.note:last-child{border-bottom:0}\n.tg{width:36px;height:20px;border-radius:10px;background:#2b2d38;position:relative;cursor:pointer;transition:background .15s}\n.tg::after{content:'';position:absolute;left:3px;top:3px;width:14px;height:14px;border-radius:50%;background:#c9ccd6;transition:transform .15s,background .15s}\n.tg.on{background:var(--acc)}.tg.on::after{transform:translateX(16px);background:#fff}\n.sl{display:flex;align-items:center;gap:8px}\n.trk{position:relative;width:108px;height:18px;margin:0 4px;cursor:pointer;touch-action:none}\n.trk::before{content:'';position:absolute;left:0;right:0;top:7px;height:4px;border-radius:2px;background:#2b2d38}\n.fill{position:absolute;left:0;top:7px;height:4px;border-radius:2px;background:var(--acc)}\n.thumb{position:absolute;top:3px;width:12px;height:12px;margin-left:-6px;border-radius:50%;background:#fff;box-shadow:0 1px 5px rgba(0,0,0,.55)}\n.vbox{width:52px;height:24px;border-radius:7px;border:1px solid var(--line2);background:#14151b;color:#cdd0da;text-align:center;font:inherit;font-size:12px;outline:0;user-select:text}\n.vbox:focus{border-color:var(--acc)}\n.dd{width:132px;height:28px;border-radius:8px;border:1px solid var(--line2);background:#14151b;color:#dfe1e9;display:flex;align-items:center;justify-content:space-between;padding:0 8px 0 10px;cursor:pointer;font-size:12.5px}\n.dd:hover{border-color:#3a3d4b}.dd .ico{color:#8b8e9b}\n.btn{height:28px;padding:0 12px;border-radius:8px;border:1px solid var(--line2);background:#1d1f28;color:#dfe1e9;cursor:pointer;font:inherit;font-size:12.5px;display:inline-flex;align-items:center;gap:6px}\n.btn:hover{background:#252835;border-color:#3a3d4b}\n.btn.acc{background:var(--acc);border-color:var(--acc);color:#fff}.btn.acc:hover{background:#5d88ff}\n.btn.danger:hover{background:#3a1d22;border-color:#8a3542;color:#ff9aa8}\n.apreset{font-size:12.5px;color:var(--mut);display:flex;align-items:center;gap:6px}.apreset b{color:var(--txt);font-weight:600}\n.pactions{display:flex;align-items:center;gap:6px;flex-wrap:wrap}\n.pdesc{font-size:11.5px;color:var(--mut);margin-top:3px}\n.seg4{display:flex;gap:6px}\n.pinbox{height:30px;flex:1;min-width:140px;border-radius:8px;border:1px solid var(--line2);background:#14151b;color:var(--txt);padding:0 10px;font:inherit;font-size:12.5px;outline:0;user-select:text}\n.pinbox:focus{border-color:var(--acc)}\n.paste-wrap{padding:10px 0;display:flex;flex-direction:column;gap:8px;border-bottom:1px solid #20222b}\n.paste-box{width:100%;height:96px;border-radius:8px;border:1px solid var(--line2);background:#14151b;color:var(--txt);padding:8px 10px;font:12px/1.4 monospace;outline:0;resize:vertical;user-select:text}\n.paste-box:focus{border-color:var(--acc)}\n.menu{position:absolute;z-index:20;background:#1a1b23;border:1px solid #2d303c;border-radius:10px;padding:4px;box-shadow:0 12px 32px rgba(0,0,0,.55);max-height:260px;overflow:auto}\n.mi{height:30px;display:flex;align-items:center;padding:0 10px;border-radius:7px;color:#cfd1db;cursor:pointer;font-size:12.5px;white-space:nowrap}\n.mi:hover{background:#262935;color:#fff}.mi.sel{color:var(--acc)}.mi.acc{color:#8fb0ff}\n.empty{grid-column:1/-1;text-align:center;color:var(--mut);padding:60px 0}\n.toast{position:absolute;left:50%;bottom:16px;transform:translate(-50%,8px);background:#232634;border:1px solid #333747;color:#e6e8f0;padding:8px 14px;border-radius:10px;font-size:12.5px;opacity:0;pointer-events:none;transition:opacity .16s,transform .16s}\n.toast.on{opacity:1;transform:translate(-50%,0)}\n.badge{font-size:11.5px;font-weight:600;padding:3px 9px;border-radius:999px;border:1px solid transparent}\n.badge.ok{color:#4ade80;border-color:rgba(74,222,128,.35);background:rgba(74,222,128,.12)}\n.badge.warn{color:#fbbf24;border-color:rgba(251,191,36,.35);background:rgba(251,191,36,.10)}\n.content.wide{grid-template-columns:1fr}.content.wide .col:empty{display:none}\n.skyrow{padding:12px 0}\n.skygrid{display:grid;grid-template-columns:repeat(4,1fr);gap:10px}\n.tile{position:relative;aspect-ratio:16/9;border:2px solid var(--line2);border-radius:10px;overflow:hidden;cursor:pointer;background:#0c0d12;transition:border-color .12s,transform .12s}\n.tile:hover{border-color:#4a4e60;transform:translateY(-1px)}\n.tile.on{border-color:var(--acc);box-shadow:0 0 0 1px var(--acc)}\n.tile canvas{display:block;width:100%;height:100%}\n.tile .tl{position:absolute;left:0;right:0;bottom:0;padding:14px 7px 4px;font-size:11px;color:#fff;background:linear-gradient(transparent,rgba(0,0,0,.78));text-shadow:0 1px 2px #000;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}\n.tile .x{position:absolute;top:4px;right:4px;width:20px;height:20px;border-radius:50%;background:rgba(0,0,0,.7);color:#fff;display:none;place-items:center;font-size:15px;line-height:1}.tile:hover .x{display:grid}.tile .x:hover{background:#c0392b}\n.tile.def{display:grid;place-items:center;color:#aeb1bd;font-size:12px;background:linear-gradient(135deg,#1a1c26,#12131a)}\n.logwrap{padding:10px 0 12px}.logbox{height:360px;overflow:auto;padding:10px 12px;border-radius:10px;border:1px solid var(--line2);background:#0e0f14;font:11.5px/1.5 ui-monospace,SFMono-Regular,Consolas,monospace;user-select:text;-webkit-user-select:text;scrollbar-width:thin;scrollbar-color:#2b2d38 transparent}\n.logl{white-space:pre-wrap;word-break:break-word;padding:1px 0;color:#cfd2dc}.logl.err{color:#ff8a96}.logl.wrn{color:#fbbf24}.logl.dbg{color:var(--mut)}\n.ptp-notify{position:fixed;top:16px;left:50%;transform:translate(-50%,-10px);z-index:2147483647;display:flex;align-items:center;gap:10px;padding:9px 14px;border-radius:12px;background:rgba(20,21,27,.94);border:1px solid #343848;color:#e9eaf0;font:12.5px/1.3 'Noto Sans','Inter',system-ui,-apple-system,'Segoe UI',sans-serif;box-shadow:0 12px 32px rgba(0,0,0,.55);opacity:0;pointer-events:none;transition:opacity .2s ease,transform .2s ease}\n.ptp-notify.on{opacity:1;transform:translate(-50%,0);pointer-events:auto;cursor:pointer}\n.ptp-notify .dot{width:8px;height:8px;border-radius:50%;background:#4f7dff;box-shadow:0 0 8px #4f7dff;flex:none}\n@media (max-width:760px){.skygrid{grid-template-columns:repeat(2,1fr)}}\n@media (max-width:760px){.side{width:64px;padding:16px 8px}.side .txt,.bname,.bsub,.grp{display:none}.brand{justify-content:center;padding:0 0 14px}.nav{justify-content:center;padding:0}.content{grid-template-columns:1fr}}";

    const host = h("div");
    host.id = "__postfx_ui";
    host.style.cssText =
      "position:fixed;inset:0;z-index:2147483000;pointer-events:none;";
    const shadow = host.attachShadow({
      mode: "open",
    });
    shadow.append(h("style", null, CSS));
    const win = h("div", {
      class: "win",
    });
    shadow.append(win);

    const side = h("div", {
      class: "side",
    });
    side.append(
      h(
        "div",
        {
          class: "brand",
        },
        h(
          "div",
          {
            class: "logo",
          },
          "PT",
        ),
        h(
          "div",
          null,
          h(
            "div",
            {
              class: "bname",
            },
            "ProTanki Pack",
          ),
          h(
            "div",
            {
              class: "bsub",
            },
            "Visuals & Animations",
          ),
        ),
      ),
    );
    const navEls = {};
    for (const t of TABS) {
      if (t.group)
        side.append(
          h(
            "div",
            {
              class: "grp",
            },
            t.group,
          ),
        );
      const n = h(
        "div",
        {
          class: "nav",
        },
        icon(t.icon),
        h(
          "span",
          {
            class: "txt",
          },
          t.name,
        ),
      );
      n.addEventListener("click", () => {
        query = "";
        searchInput.value = "";
        searchBox.classList.remove("on");
        tab = t.id;
        ls.set(LSK.tab, tab);
        render();
      });
      navEls[t.id] = n;
      side.append(n);
    }
    side.append(
      h("div", {
        class: "sp",
      }),
    );

    const main = h("div", {
      class: "main",
    });
    const top = h("div", {
      class: "top",
    });
    const activePresetLbl = h("div", {
      class: "apreset",
    });
    const searchBox = h("div", {
      class: "srch",
    });
    const searchInput = h("input", {
      placeholder: "Search settings",
      spellcheck: "false",
    });
    const searchBtn = h(
      "div",
      {
        class: "pbtn",
      },
      icon("search"),
    );
    searchBox.append(searchInput, searchBtn);
    top.append(
      activePresetLbl,
      h("div", {
        class: "sp",
      }),
      searchBox,
    );
    const content = h("div", {
      class: "content",
    });
    const toastEl = h("div", {
      class: "toast",
    });
    main.append(top, content, toastEl);
    win.append(side, main);

    // keep the game from seeing clicks, scrolls and (typed) keys that belong to the menu
    [
      "mousedown",
      "mouseup",
      "click",
      "dblclick",
      "contextmenu",
      "wheel",
      "pointerdown",
      "pointerup",
      "mousemove",
      "pointermove",
    ].forEach((ev) => win.addEventListener(ev, (e) => e.stopPropagation()));
    ["keydown", "keyup", "keypress"].forEach((ev) =>
      win.addEventListener(ev, (e) => {
        if (e.target && e.target.tagName === "INPUT") e.stopPropagation();
      }),
    );

    let toastT = 0;
    const toast = (msg) => {
      toastEl.textContent = msg;
      toastEl.classList.add("on");
      clearTimeout(toastT);
      toastT = setTimeout(() => toastEl.classList.remove("on"), 1700);
    };

    // ---------- popup menu (used by dropdowns and presets) ----------
    let menuEl = null,
      menuAnchor = null;
    const closeMenu = () => {
      if (menuEl) {
        menuEl.remove();
        menuEl = null;
        menuAnchor = null;
      }
    };
    const openMenu = (anchor, items, selected, onPick) => {
      if (menuEl && menuAnchor === anchor) {
        closeMenu();
        return;
      }
      closeMenu();
      const wr = win.getBoundingClientRect(),
        ar = anchor.getBoundingClientRect();
      const m = h("div", {
        class: "menu",
      });
      m.style.left = ar.left - wr.left + "px";
      m.style.top = ar.bottom - wr.top + 4 + "px";
      m.style.minWidth = ar.width + "px";
      for (const it of items) {
        const b = h(
          "div",
          {
            class:
              "mi" +
              (it.value === selected ? " sel" : "") +
              (it.cls ? " " + it.cls : ""),
          },
          it.label,
        );
        b.addEventListener("click", () => {
          closeMenu();
          onPick(it.value);
        });
        m.append(b);
      }
      win.append(m);
      menuEl = m;
      menuAnchor = anchor;
      const mr = m.getBoundingClientRect();
      if (mr.bottom > wr.bottom - 8)
        m.style.top = Math.max(8, ar.top - wr.top - mr.height - 4) + "px";
      if (mr.right > wr.right - 8)
        m.style.left = Math.max(8, ar.right - wr.left - mr.width) + "px";
    };
    win.addEventListener("pointerdown", (e) => {
      if (
        menuEl &&
        !e.composedPath().includes(menuEl) &&
        !e.composedPath().includes(menuAnchor)
      )
        closeMenu();
    });

    // ---------- controls ----------
    let tab = ls.get(LSK.tab, "weather");
    if (!TABS.some((t) => t.id === tab)) tab = "weather";
    let query = "";
    const changed = () => {
      persist();
      refresh();
    };
    const makeRow = (c) => {
      if (c.t === "note")
        return h(
          "div",
          {
            class: "note",
          },
          c.text,
        );
      if (c.t === "dynnote") {
        const el = h("div", {
          class: "note",
        });
        UPD.push(() => {
          el.textContent = c.fn();
        });
        return el;
      }
      if (c.t === "skygrid") {
        const grid = h("div", {
            class: "skygrid",
          }),
          tiles = new Map(),
          wrap = h(
            "div",
            {
              class: "skyrow",
            },
            grid,
          );
        const addTile = (key, name, thumbP, x) => {
          const el = h("div", {
            class: "tile" + (thumbP ? "" : " def"),
            title: name,
          });
          if (thumbP) {
            el.append(
              h(
                "div",
                {
                  class: "tl",
                },
                name,
              ),
            );
            thumbP
              .then((cv) => {
                const k = h("canvas");
                k.width = cv.width;
                k.height = cv.height;
                k.getContext("2d").drawImage(cv, 0, 0);
                el.prepend(k);
              })
              .catch(() => {});
          } else el.append(h("span", null, name));
          if (x && x.custom) {
            const rm = h(
              "div",
              {
                class: "x",
                title: "Remove this sky",
              },
              "\u00d7",
            );
            rm.addEventListener("click", (e) => {
              e.stopPropagation();
              SKYLIB.remove(x.key).then(() => {
                if (O.sky.set === x.key) {
                  set(c, "");
                  changed();
                }
                toast('Removed "' + name + '"');
                render();
              });
            });
            el.append(rm);
          }
          el.addEventListener("click", () => {
            set(c, key);
            changed();
            toast(key ? "Sky: " + name : "Sky: map default");
          });
          tiles.set(key, el);
          grid.append(el);
        };
        addTile("", "Map default", null);
        SKYLIB.avail().then((list) => {
          if (!wrap.isConnected) return;
          for (const x of list) addTile(x.key, x.name, SKYLIB.thumb(x), x);
          if (!list.length)
            wrap.append(
              h(
                "div",
                {
                  class: "note",
                },
                "No skyboxes found in the game cache yet. They are stored when the game downloads its resources: play a battle or two, then reopen this tab.",
              ),
            );
          refresh();
        });
        UPD.push(() =>
          tiles.forEach((el, k) => el.classList.toggle("on", get(c) === k)),
        );
        return wrap;
      }
      const row = h("div", {
        class: "row",
      });
      const ctl = h("div", {
        class: "ctl",
      });
      row.append(
        h(
          "div",
          {
            class: "lab",
            title: c.label,
          },
          c.label,
        ),
        ctl,
      );
      const dim = () => row.classList.toggle("dim", !!c.en && !c.en());
      if (c.t === "toggle") {
        const t = h("div", {
          class: "tg",
        });
        ctl.append(t);
        t.addEventListener("click", () => {
          set(c, !get(c));
          changed();
        });
        UPD.push(() => {
          t.classList.toggle("on", !!get(c));
          dim();
        });
      } else if (c.t === "slider") {
        const dec = decimals(c.step);
        const fmt = (v) => v.toFixed(dec) + (c.unit || "");
        const trk = h("div", {
            class: "trk",
          }),
          fill = h("div", {
            class: "fill",
          }),
          thumb = h("div", {
            class: "thumb",
          });
        trk.append(fill, thumb);
        const box = h("input", {
          class: "vbox",
          spellcheck: "false",
        });
        ctl.append(
          h(
            "div",
            {
              class: "sl",
            },
            trk,
            box,
          ),
        );
        const move = (e) => {
          const r = trk.getBoundingClientRect();
          let v =
            c.min +
            clamp((e.clientX - r.left) / r.width, 0, 1) * (c.max - c.min);
          v = +(Math.round(v / c.step) * c.step).toFixed(6);
          set(c, clamp(v, c.min, c.max));
          changed();
        };
        let drag = false;
        trk.addEventListener("pointerdown", (e) => {
          drag = true;
          trk.setPointerCapture(e.pointerId);
          move(e);
        });
        trk.addEventListener("pointermove", (e) => {
          if (drag) move(e);
        });
        trk.addEventListener("pointerup", () => {
          drag = false;
        });
        trk.addEventListener("pointercancel", () => {
          drag = false;
        });
        const commit = () => {
          const v = parseFloat(box.value);
          if (isFinite(v)) {
            set(
              c,
              clamp(
                +(Math.round(v / c.step) * c.step).toFixed(6),
                c.min,
                c.max,
              ),
            );
          }
          changed();
          box.value = fmt(get(c));
        };
        box.addEventListener("focus", () => box.select());
        box.addEventListener("keydown", (e) => {
          if (e.key === "Enter") {
            commit();
            box.blur();
          } else if (e.key === "Escape") {
            box.value = fmt(get(c));
            box.blur();
          }
        });
        box.addEventListener("blur", commit);
        UPD.push(() => {
          const v = get(c),
            q = clamp((v - c.min) / (c.max - c.min), 0, 1);
          fill.style.width = q * 100 + "%";
          thumb.style.left = q * 100 + "%";
          if (shadow.activeElement !== box) box.value = fmt(v);
          dim();
        });
      } else if (c.t === "select") {
        const getOpts = () =>
          typeof c.opts === "function" ? c.opts() : c.opts;
        const label = h("span");
        const dd = h(
          "div",
          {
            class: "dd",
          },
          label,
          icon("chev", 15),
        );
        ctl.append(dd);
        dd.addEventListener("click", () =>
          openMenu(
            dd,
            getOpts().map((o) => ({
              value: o[0],
              label: o[1],
            })),
            get(c),
            (v) => {
              set(c, v);
              changed();
            },
          ),
        );
        UPD.push(() => {
          const o = getOpts().find((x) => x[0] === get(c));
          label.textContent = o ? o[1] : String(get(c));
          dim();
        });
      } else if (c.t === "status") {
        const badge = h("span", {
          class: "badge warn",
        });
        ctl.append(badge);
        UPD.push(() => {
          const st = c.fn();
          badge.textContent = st.text;
          badge.className = "badge " + (st.ready ? "ok" : "warn");
        });
      } else if (c.t === "button") {
        const b = h(
          "button",
          {
            class: "btn",
          },
          c.btn,
        );
        ctl.append(b);
        b.addEventListener("click", c.fn);
      } else if (c.t === "keybind") {
        const b = h("button", {
          class: "btn",
        });
        if (c.disabled) {
          b.disabled = true;
          b.style.opacity = "0.45";
          b.style.cursor = "not-allowed";
          if (c.disabledReason) b.title = c.disabledReason;
        }
        ctl.append(b);
        b.addEventListener("click", () => {
          if (c.disabled) {
            if (c.disabledReason && typeof notifyInGame === "function") {
              notifyInGame(c.disabledReason, true);
            }
            return;
          }
          binding = c.bindId;
          b.textContent = "Press a key...";
          b.blur();
        });
        UPD.push(() => {
          if (binding === c.bindId) return;
          const val = c.bindId === "menu" ? menuKey : keybinds[c.bindId] || "";
          b.textContent = val ? formatBind(val) : "None";
        });
      }
      return row;
    };
    let binding = null;
    const formatBind = (b) =>
      b
        ? String(b)
            .split("+")
            .map((p) => keyName(p))
            .join(" + ")
        : "None";
    const isModCode = (code) =>
      code === "ControlLeft" ||
      code === "ControlRight" ||
      code === "ShiftLeft" ||
      code === "ShiftRight" ||
      code === "AltLeft" ||
      code === "AltRight" ||
      code === "MetaLeft" ||
      code === "MetaRight";
    const encodeBind = (e) =>
      (e.ctrlKey ? "Ctrl+" : "") +
      (e.altKey ? "Alt+" : "") +
      (e.shiftKey ? "Shift+" : "") +
      e.code;
    const matchesBind = (e, b) => {
      if (!b) return false;
      const parts = b.split("+"),
        code = parts.pop();
      return (
        e.code === code &&
        e.ctrlKey === parts.includes("Ctrl") &&
        e.altKey === parts.includes("Alt") &&
        e.shiftKey === parts.includes("Shift")
      );
    };

    const tabName = (id) => (TABS.find((t) => t.id === id) || {}).name;
    let pasteOpen = false;

    function renderLogsTab() {
      content.classList.add("wide");
      const col = h("div", { class: "col" });
      const sec = h("div", { class: "sec" }, h("h4", null, "TextureSwapper Logs"));
      const card = h("div", { class: "card" });

      const statusBadge = h("span", { class: "badge warn" }, "Checking");
      const fileSub = h("div", { class: "pdesc" }, "Waiting for log status...");
      const refreshBtn = h("button", { class: "btn acc" }, "Refresh");
      const copyBtn = h("button", { class: "btn" }, "Copy");
      const bottomBtn = h("button", { class: "btn" }, "Bottom");

      card.append(
        h(
          "div",
          { class: "row" },
          h(
            "div",
            { class: "lab" },
            h(
              "div",
              { class: "apreset" },
              h("b", null, "Client & Swapper Log"),
              statusBadge,
            ),
            fileSub,
          ),
          h("div", { class: "pactions" }, refreshBtn, copyBtn, bottomBtn),
        ),
      );

      let activeLevel = "all";
      let filterText = "";
      let rawContent = "";

      const levelSeg = h("div", { class: "seg4" });
      const levelBtns = {};
      for (const [lv, lbl] of [
        ["all", "All"],
        ["INF", "Info"],
        ["WRN", "Warn"],
        ["ERR", "Error"],
      ]) {
        const lb = h("button", { class: "btn" + (lv === "all" ? " acc" : "") }, lbl);
        lb.addEventListener("click", () => {
          activeLevel = lv;
          for (const k in levelBtns) {
            levelBtns[k].className = "btn" + (k === activeLevel ? " acc" : "");
          }
          paintLines();
        });
        levelBtns[lv] = lb;
        levelSeg.append(lb);
      }

      const filterInp = h("input", {
        class: "pinbox",
        placeholder: "Filter log lines...",
        spellcheck: "false",
      });
      filterInp.addEventListener("input", () => {
        filterText = filterInp.value.trim().toLowerCase();
        paintLines();
      });

      card.append(h("div", { class: "row" }, levelSeg, filterInp));

      const logBox = h("div", { class: "logbox" });
      card.append(h("div", { class: "logwrap" }, logBox));

      const paintLines = () => {
        logBox.textContent = "";
        const lines = (rawContent || "").split(/\r?\n/);
        const frag = document.createDocumentFragment();
        let shown = 0;
        for (const line of lines) {
          if (!line) continue;
          const isErr = /\[\s*(ERR|FTL)\s*\]|\bError\b/i.test(line);
          const isWrn = /\[\s*WRN\s*\]|\bWarning\b/i.test(line);
          const isDbg = /\[\s*DBG\s*\]/i.test(line);
          if (activeLevel === "ERR" && !isErr) continue;
          if (activeLevel === "WRN" && !isWrn) continue;
          if (activeLevel === "INF" && (isErr || isWrn || isDbg)) continue;
          if (filterText && !line.toLowerCase().includes(filterText)) continue;
          const cls = isErr
            ? "logl err"
            : isWrn
              ? "logl wrn"
              : isDbg
                ? "logl dbg"
                : "logl";
          frag.append(h("div", { class: cls }, line));
          shown++;
        }
        if (!shown) {
          frag.append(
            h("div", { class: "logl dbg" }, rawContent ? "No matching log lines." : "(empty log)"),
          );
        }
        logBox.append(frag);
        logBox.scrollTop = logBox.scrollHeight;
      };

      const loadLogs = () => {
        if (!HOST_IPC_TOKEN || isStandaloneBuild) {
          statusBadge.className = "badge warn";
          statusBadge.textContent = "Standalone";
          fileSub.textContent = "Launch ProTanki via TextureSwapper to view live swapper logs.";
          rawContent = "";
          paintLines();
          refreshBtn.disabled = true;
          refreshBtn.style.opacity = "0.45";
          refreshBtn.style.cursor = "not-allowed";
          return;
        }
        statusBadge.className = "badge warn";
        statusBadge.textContent = "Loading";
        fetch("http://127.0.0.1:47921/ptpack-logs", {
          cache: "no-store",
          headers: { "X-PTPack-Token": HOST_IPC_TOKEN },
        })
          .then((r) => (r.ok ? r.json() : null))
          .then((d) => {
            if (!d) {
              statusBadge.className = "badge warn";
              statusBadge.textContent = "Offline";
              fileSub.textContent = "Could not reach TextureSwapper on port 47921.";
              rawContent = "";
              paintLines();
              return;
            }
            statusBadge.className = "badge ok";
            statusBadge.textContent = "Connected";
            fileSub.textContent = d.file ? "File: " + d.file : "Current session";
            rawContent = d.content || "";
            paintLines();
          })
          .catch(() => {
            statusBadge.className = "badge warn";
            statusBadge.textContent = "Offline";
            fileSub.textContent = "Could not reach TextureSwapper on port 47921.";
            rawContent = "";
            paintLines();
          });
      };

      refreshBtn.addEventListener("click", loadLogs);
      bottomBtn.addEventListener("click", () => {
        logBox.scrollTop = logBox.scrollHeight;
      });
      copyBtn.addEventListener("click", () => {
        try {
          navigator.clipboard
            .writeText(rawContent || "")
            .then(() => toast("Logs copied"))
            .catch(() => toast("Copy failed"));
        } catch (e) {
          toast("Copy failed");
        }
      });

      sec.append(card);
      col.append(sec);
      content.append(col);
      loadLogs();
    }

    function renderPresetsTab() {
      content.classList.add("wide");
      const col = h("div", {
        class: "col",
      });

      const qualCard = h("div", {
        class: "card",
      });
      const qualSeg = h("div", {
        class: "seg4",
      });
      for (const [qk, qm] of Object.entries(QUALITY_TIERS)) {
        const qb = h(
          "button",
          {
            class: "btn" + (qualityTier === qk ? " acc" : ""),
          },
          qm.label,
        );
        qb.addEventListener("click", () => {
          qualityTier = qk;
          ls.set(LSK.quality, qk);
          const builtin = BUILTIN_PRESETS.find((p) => p.name === presetName);
          const base =
            presetName === "Default"
              ? VISUAL_FACTORY
              : builtin
                ? builtin.settings
                : snapshot();
          applyDict(scaleDictByQuality(base, qk));
          toast("Quality: " + qm.label);
          render();
        });
        qualSeg.append(qb);
      }
      qualCard.append(
        h(
          "div",
          {
            class: "row",
          },
          h(
            "div",
            {
              class: "lab",
            },
            h("div", null, "Graphical Quality"),
            h(
              "div",
              {
                class: "pdesc",
              },
              "Scales particle counts, blur/DoF samples, wet/snow range, and light rays.",
            ),
          ),
          qualSeg,
        ),
      );
      col.append(
        h(
          "div",
          {
            class: "sec",
          },
          h("h4", null, "Preset Quality"),
          qualCard,
        ),
      );

      const builtinCard = h("div", {
        class: "card",
      });
      const addBuiltinRow = (name, desc, baseDict) => {
        const isActive = presetName === name;
        const left = h(
          "div",
          {
            class: "lab",
          },
          h(
            "div",
            {
              style: "display:flex;align-items:center;gap:8px",
            },
            h("span", null, name),
            isActive
              ? h(
                  "span",
                  {
                    class: "badge ok",
                  },
                  "Active",
                )
              : null,
          ),
          desc
            ? h(
                "div",
                {
                  class: "pdesc",
                },
                desc,
              )
            : null,
        );
        const acts = h("div", {
          class: "pactions",
        });
        const loadBtn = h(
          "button",
          {
            class: "btn" + (isActive ? "" : " acc"),
          },
          "Load",
        );
        loadBtn.addEventListener("click", () => {
          applyDict(scaleDictByQuality(baseDict, qualityTier));
          setPresetName(name);
          toast(
            'Loaded "' + name + '" (' + QUALITY_TIERS[qualityTier].label + ")",
          );
          render();
        });
        const shareBtn = h(
          "button",
          {
            class: "btn",
            title: "Download .json & copy to clipboard",
          },
          icon("share", 14),
          "Share",
        );
        shareBtn.addEventListener("click", () =>
          exportPreset(name, scaleDictByQuality(baseDict, qualityTier)),
        );
        acts.append(loadBtn, shareBtn);
        builtinCard.append(
          h(
            "div",
            {
              class: "row",
            },
            left,
            acts,
          ),
        );
      };
      addBuiltinRow(
        "Default",
        "Standard ProTanki Pack factory defaults.",
        VISUAL_FACTORY,
      );
      for (const bp of BUILTIN_PRESETS)
        addBuiltinRow(bp.name, bp.desc, bp.settings);
      col.append(
        h(
          "div",
          {
            class: "sec",
          },
          h("h4", null, "Built-in Presets"),
          builtinCard,
        ),
      );

      const createCard = h("div", {
        class: "card",
      });
      const nameInp = h("input", {
        class: "pinbox",
        placeholder: "New preset name...",
        spellcheck: "false",
        maxlength: "24",
      });
      const saveNewBtn = h(
        "button",
        {
          class: "btn acc",
        },
        "Save New",
      );
      const doSaveNew = () => {
        const name = nameInp.value.trim();
        if (
          !name ||
          name === "Default" ||
          BUILTIN_PRESETS.some((p) => p.name === name)
        ) {
          toast("Enter a unique preset name");
          return;
        }
        presets[name] = snapshot();
        ls.set(LSK.presets, presets);
        setPresetName(name);
        nameInp.value = "";
        toast('Saved preset "' + name + '"');
        render();
      };
      saveNewBtn.addEventListener("click", doSaveNew);
      nameInp.addEventListener("keydown", (e) => {
        if (e.key === "Enter") doSaveNew();
      });
      createCard.append(
        h(
          "div",
          {
            class: "row",
          },
          h(
            "div",
            {
              class: "lab",
            },
            "Create preset from current settings",
          ),
          h(
            "div",
            {
              class: "pactions",
            },
            nameInp,
            saveNewBtn,
          ),
        ),
      );

      const impFileBtn = h(
        "button",
        {
          class: "btn",
        },
        icon("upload", 14),
        "Import .json",
      );
      impFileBtn.addEventListener("click", () => importPresetFile());
      const pasteBtn = h(
        "button",
        {
          class: "btn",
        },
        pasteOpen ? "Hide Paste" : "Paste JSON",
      );
      pasteBtn.addEventListener("click", () => {
        pasteOpen = !pasteOpen;
        render();
      });
      const resetDefBtn = h(
        "button",
        {
          class: "btn",
        },
        "Reset to Default",
      );
      resetDefBtn.addEventListener("click", () => {
        applyDict(scaleDictByQuality(FACTORY, qualityTier));
        setPresetName("Default");
        toast("Loaded Default settings");
        render();
      });
      createCard.append(
        h(
          "div",
          {
            class: "row",
          },
          h(
            "div",
            {
              class: "lab",
            },
            "Import or reset",
          ),
          h(
            "div",
            {
              class: "pactions",
            },
            impFileBtn,
            pasteBtn,
            resetDefBtn,
          ),
        ),
      );

      if (pasteOpen) {
        const ta = h("textarea", {
          class: "paste-box",
          placeholder: "Paste shared preset JSON here...",
          spellcheck: "false",
        });
        const applyPasteBtn = h(
          "button",
          {
            class: "btn acc",
          },
          "Import & Apply JSON",
        );
        applyPasteBtn.addEventListener("click", () => {
          if (!ta.value.trim()) {
            toast("Paste preset JSON first");
            return;
          }
          importPresetJson(ta.value.trim(), "Shared");
          pasteOpen = false;
          render();
        });
        createCard.append(
          h(
            "div",
            {
              class: "paste-wrap",
            },
            ta,
            h(
              "div",
              {
                class: "pactions",
              },
              applyPasteBtn,
            ),
          ),
        );
      }
      col.append(
        h(
          "div",
          {
            class: "sec",
          },
          h("h4", null, "Create & Import"),
          createCard,
        ),
      );

      const listCard = h("div", {
        class: "card",
      });
      const addPresetRow = (name) => {
        const isActive = presetName === name;
        const left = h(
          "div",
          {
            class: "lab",
            style: "display:flex;align-items:center;gap:8px",
          },
          h("span", null, name),
          isActive
            ? h(
                "span",
                {
                  class: "badge ok",
                },
                "Active",
              )
            : null,
        );
        const acts = h("div", {
          class: "pactions",
        });
        const loadBtn = h(
          "button",
          {
            class: "btn" + (isActive ? "" : " acc"),
          },
          "Load",
        );
        loadBtn.addEventListener("click", () => {
          applyDict(presets[name]);
          setPresetName(name);
          toast('Loaded "' + name + '"');
          render();
        });
        const updBtn = h(
          "button",
          {
            class: "btn",
            title: "Overwrite with current settings",
          },
          "Update",
        );
        updBtn.addEventListener("click", () => {
          presets[name] = snapshot();
          ls.set(LSK.presets, presets);
          setPresetName(name);
          toast('Updated "' + name + '"');
          render();
        });
        const shareBtn = h(
          "button",
          {
            class: "btn",
            title: "Download .json & copy to clipboard",
          },
          icon("share", 14),
          "Share",
        );
        shareBtn.addEventListener("click", () =>
          exportPreset(name, presets[name]),
        );
        const delBtn = h(
          "button",
          {
            class: "btn danger",
            title: "Delete preset",
          },
          icon("trash", 14),
        );
        delBtn.addEventListener("click", () => {
          delete presets[name];
          ls.set(LSK.presets, presets);
          if (presetName === name) setPresetName("Default");
          toast('Deleted "' + name + '"');
          render();
        });
        acts.append(loadBtn, updBtn, shareBtn, delBtn);
        listCard.append(
          h(
            "div",
            {
              class: "row",
            },
            left,
            acts,
          ),
        );
      };
      const names = Object.keys(presets);
      for (const n of names) addPresetRow(n);
      if (!names.length)
        listCard.append(
          h(
            "div",
            {
              class: "note",
            },
            "No custom presets saved yet. Create one above or import a shared .json file.",
          ),
        );
      col.append(
        h(
          "div",
          {
            class: "sec",
          },
          h("h4", null, "Your Saved Presets (" + names.length + ")"),
          listCard,
        ),
      );

      content.append(col);
      refresh();
    }

    function render() {
      if (!uiReady) return;
      closeMenu();
      UPD = [];
      content.replaceChildren();
      content.scrollTop = 0;
      for (const id in navEls)
        navEls[id].classList.toggle("on", !query && id === tab);
      const q = query.trim().toLowerCase();
      if (!q && tab === "presets") {
        renderPresetsTab();
        return;
      }
      if (!q && tab === "logs") {
        renderLogsTab();
        return;
      }
      const list = q
        ? C.filter(
            (c) =>
              c.t !== "hidden" &&
              c.label &&
              (c.label + " " + c.sec + " " + tabName(c.tab))
                .toLowerCase()
                .includes(q),
          )
        : C.filter((c) => c.t !== "hidden" && c.tab === tab);
      if (!list.length) {
        content.append(
          h(
            "div",
            {
              class: "empty",
            },
            "No settings found",
          ),
        );
        return;
      }
      const cols = [
        h("div", {
          class: "col",
        }),
        h("div", {
          class: "col",
        }),
      ];
      const groups = [],
        map = new Map();
      for (const c of list) {
        const key = c.tab + "|" + c.sec;
        let g = map.get(key);
        if (!g) {
          g = {
            title: q ? tabName(c.tab) + " / " + c.sec : c.sec,
            col: c.col,
            items: [],
          };
          map.set(key, g);
          groups.push(g);
        }
        g.items.push(c);
      }
      let alt = 0;
      for (const g of groups) {
        const card = h("div", {
          class: "card",
        });
        for (const c of g.items) card.append(makeRow(c));
        cols[q ? alt++ % 2 : g.col].append(
          h(
            "div",
            {
              class: "sec",
            },
            h("h4", null, g.title),
            card,
          ),
        );
      }
      content.append(cols[0], cols[1]);
      content.classList.toggle("wide", !q && tab === "sky"); // the sky tab uses the whole width for its thumbnails
      refresh();
    }

    // ---------- presets & search ----------
    const setPresetName = (n) => {
      presetName = n;
      ls.set(LSK.preset, n);
      paintPreset();
    };
    const paintPreset = () => {
      activePresetLbl.replaceChildren("Preset: ", h("b", null, presetName));
    };
    const exportPreset = (name, data) => {
      const pName = name && name !== "Default" ? name : "Default";
      const payload = {
        name: pName,
        version: 1,
        pack: "ProTanki Pack",
        settings: data || snapshot(),
      };
      const json = JSON.stringify(payload, null, 2);
      try {
        const blob = new Blob([json], {
          type: "application/json",
        });
        const url = URL.createObjectURL(blob);
        const a = h("a", {
          href: url,
          download: pName.replace(/[^a-z0-9_\- ]/gi, "_") + ".json",
        });
        a.style.display = "none";
        (document.body || document.documentElement).append(a);
        a.click();
        a.remove();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
      } catch (e) {}
      try {
        if (navigator.clipboard && navigator.clipboard.writeText)
          navigator.clipboard.writeText(json);
      } catch (e) {}
      toast('Exported "' + pName + '.json" & copied to clipboard');
    };
    const importPresetJson = (rawText, fallbackName) => {
      try {
        const parsed = JSON.parse(rawText);
        if (!parsed || typeof parsed !== "object" || Array.isArray(parsed))
          throw new Error("Invalid JSON");
        const settings =
          parsed.settings && typeof parsed.settings === "object"
            ? parsed.settings
            : parsed;
        let name =
          typeof parsed.name === "string" && parsed.name.trim()
            ? parsed.name.trim()
            : fallbackName || "Imported";
        name = name.replace(/\.json$/i, "").slice(0, 24);
        if (!name || name === "Default") name = "Imported";
        applyDict(settings);
        presets[name] = snapshot();
        ls.set(LSK.presets, presets);
        setPresetName(name);
        toast('Imported preset "' + name + '"');
        if (tab === "presets") render();
      } catch (e) {
        toast("Invalid preset JSON");
      }
    };
    const importPresetFile = () => {
      const inp = h("input", {
        type: "file",
        accept: ".json,application/json",
      });
      inp.style.display = "none";
      inp.addEventListener("change", () => {
        const f = inp.files && inp.files[0];
        inp.remove();
        if (!f) return;
        const r = new FileReader();
        r.onload = () => importPresetJson(String(r.result || ""), f.name);
        r.readAsText(f);
      });
      (document.body || document.documentElement).append(inp);
      inp.click();
    };
    searchBtn.addEventListener("click", () => {
      const on = !searchBox.classList.contains("on");
      searchBox.classList.toggle("on", on);
      if (on) searchInput.focus();
      else {
        query = "";
        searchInput.value = "";
        render();
      }
    });
    searchInput.addEventListener("input", () => {
      query = searchInput.value;
      render();
    });
    searchInput.addEventListener("keydown", (e) => {
      if (e.key === "Escape") {
        query = "";
        searchInput.value = "";
        searchBox.classList.remove("on");
        searchInput.blur();
        render();
      }
    });

    // drag the window by its top bar
    const savedPos = ls.get(LSK.pos, null);
    let drag = null,
      dx = savedPos && typeof savedPos.dx === "number" ? savedPos.dx : 0,
      dy = savedPos && typeof savedPos.dy === "number" ? savedPos.dy : 0;
    const clampPos = (persist) => {
      const vw = window.innerWidth || document.documentElement.clientWidth || 0;
      const vh =
        window.innerHeight || document.documentElement.clientHeight || 0;
      const w = win.offsetWidth,
        h = win.offsetHeight;
      if (vw > 0 && vh > 0 && w > 0 && h > 0) {
        const maxDx = Math.max(0, (vw - w) * 0.5);
        const maxDy = Math.max(0, (vh - h) * 0.5);
        const nx = clamp(dx, -maxDx, maxDx),
          ny = clamp(dy, -maxDy, maxDy);
        if (persist && (nx !== dx || ny !== dy))
          ls.set(LSK.pos, {
            dx: nx,
            dy: ny,
          });
        dx = nx;
        dy = ny;
      }
      win.style.setProperty("--dx", dx + "px");
      win.style.setProperty("--dy", dy + "px");
    };
    if (dx || dy) {
      win.style.setProperty("--dx", dx + "px");
      win.style.setProperty("--dy", dy + "px");
    }
    top.addEventListener("pointerdown", (e) => {
      if (
        e.target !== top &&
        !e.target.classList.contains("sp") &&
        !e.target.closest(".apreset")
      )
        return;
      drag = {
        x: e.clientX,
        y: e.clientY,
        dx,
        dy,
      };
      top.setPointerCapture(e.pointerId);
    });
    top.addEventListener("pointermove", (e) => {
      if (!drag) return;
      dx = drag.dx + e.clientX - drag.x;
      dy = drag.dy + e.clientY - drag.y;
      clampPos(false);
    });
    const endDrag = () => {
      if (drag) {
        drag = null;
        clampPos(false);
        ls.set(LSK.pos, {
          dx,
          dy,
        });
      }
    };
    top.addEventListener("pointerup", endDrag);
    top.addEventListener("pointercancel", endDrag);
    window.addEventListener("resize", () => {
      if (isOpen) clampPos(true);
    });

    // ---------- open / close & keybinds ----------
    let isOpen = false,
      poll = 0,
      hideT = 0;
    const open = () => {
      if (isOpen) return;
      isOpen = true;
      clearTimeout(hideT);
      if (!host.isConnected)
        (document.body || document.documentElement).append(host);
      win.classList.add("show");
      clampPos(true);
      render();
      requestAnimationFrame(() => win.classList.add("open"));
      poll = setInterval(() => {
        if (!drag) refresh();
      }, 400);
    };
    const close = () => {
      if (!isOpen) return;
      isOpen = false;
      clearInterval(poll);
      closeMenu();
      binding = null;
      try {
        shadow.activeElement && shadow.activeElement.blur();
      } catch (e) {}
      win.classList.remove("open");
      hideT = setTimeout(() => win.classList.remove("show"), 160);
    };
    const toggle = () => (isOpen ? close() : open());
    const KEYBIND_ACTIONS = {
      precipCycle: () => {
        const o = ["off", "rain", "snow"];
        O.precip.type = o[(o.indexOf(O.precip.type) + 1) % 3];
        toast("Precipitation: " + O.precip.type);
      },
      snowg: () => {
        O.snowg.enabled = !O.snowg.enabled;
        toast("Snow ground: " + (O.snowg.enabled ? "ON" : "OFF"));
      },
      wet: () => {
        O.wet.enabled = !O.wet.enabled;
        toast("Wet ground: " + (O.wet.enabled ? "ON" : "OFF"));
      },
      bloom: () => {
        O.bloom.enabled = !O.bloom.enabled;
        toast("Bloom: " + (O.bloom.enabled ? "ON" : "OFF"));
      },
      cam: () => {
        O.cam.enabled = !O.cam.enabled;
        toast("Motion blur: " + (O.cam.enabled ? "ON" : "OFF"));
      },
      dof: () => {
        O.dof.enabled = !O.dof.enabled;
        toast("Depth of field: " + (O.dof.enabled ? "ON" : "OFF"));
      },
      grade: () => {
        O.grade.enabled = !O.grade.enabled;
        toast("Color grading: " + (O.grade.enabled ? "ON" : "OFF"));
      },
      sharp: () => {
        O.sharp.enabled = !O.sharp.enabled;
        toast("Sharpening: " + (O.sharp.enabled ? "ON" : "OFF"));
      },
      fog: () => {
        O.fog.enabled = !O.fog.enabled;
        toast("Fog: " + (O.fog.enabled ? "ON" : "OFF"));
      },
      sun: () => {
        O.sun.enabled = !O.sun.enabled;
        toast("Sun: " + (O.sun.enabled ? "ON" : "OFF"));
      },
      light: () => {
        O.light.enabled = !O.light.enabled;
        toast("Lightning: " + (O.light.enabled ? "ON" : "OFF"));
      },
      strike: () => {
        if (O.light.enabled) {
          O.light.now = 1;
          toast("Lightning strike!");
        } else toast("Turn lightning on first");
      },
      hudCenter: () => {
        O.hud.center = !O.hud.center;
        hudApply();
        toast("Centered HUD: " + (O.hud.center ? "ON" : "OFF"));
      },
      hudClassic: () => {
        O.hud.classic = !O.hud.classic;
        hudApply();
        toast("Classic supply bar: " + (O.hud.classic ? "ON" : "OFF"));
      },
      animTrigger: () => {
        if (!animations) return;
        O.anim.trigger = O.anim.trigger === "shoot" ? "always" : "shoot";
        animSync();
        toast(
          "Animation trigger: " +
            (O.anim.trigger === "always" ? "Always" : "On Shoot"),
        );
      },
    };
    const isInputFocused = () => {
      const a = shadow.activeElement || document.activeElement;
      return (
        a &&
        (a.tagName === "INPUT" ||
          a.tagName === "TEXTAREA" ||
          a.isContentEditable)
      );
    };
    const onKey = (e) => {
      if (binding) {
        e.preventDefault();
        e.stopPropagation();
        if (isModCode(e.code)) return;
        const target = binding;
        binding = null;
        if (e.code !== "Escape") {
          if (target === "menu") {
            if (!e.ctrlKey && !e.altKey && !e.shiftKey) {
              const conflict = findKeyConflict(e.code, "menu");
              if (conflict) {
                toast("Key already used by: " + conflict);
              } else {
                menuKey = e.code;
                ls.set(LSK.key, menuKey);
                ls.set(LSK.hostKey, menuKey);
                toast("Menu key: " + keyName(menuKey));
                syncMenuHotkeyToHost(menuKey, true);
              }
            } else toast("Menu toggle uses a single key");
          } else {
            const unbind =
              !e.ctrlKey &&
              !e.altKey &&
              !e.shiftKey &&
              (e.code === "Backspace" || e.code === "Delete");
            if (unbind) {
              keybinds[target] = "";
              ls.set(LSK.keybinds, keybinds);
              toast("Keybind cleared");
            } else {
              const nextBind = encodeBind(e);
              const conflict = findKeyConflict(nextBind, target);
              if (conflict) {
                toast("Key already used by: " + conflict);
              } else {
                keybinds[target] = nextBind;
                ls.set(LSK.keybinds, keybinds);
                toast("Bound to " + formatBind(keybinds[target]));
              }
            }
          }
        }
        refresh();
        return;
      }
      if (e.repeat || isInputFocused()) return;
      if (e.code === menuKey && !e.ctrlKey && !e.altKey && !e.shiftKey) {
        e.preventDefault();
        e.stopPropagation();
        toggle();
        return;
      }
      for (const id in KEYBIND_ACTIONS) {
        if (matchesBind(e, keybinds[id])) {
          KEYBIND_ACTIONS[id]();
          persist();
          refresh();
          break;
        }
      }
    };
    const isShadowInputFocused = () => {
      const a = shadow.activeElement;
      return !!(
        a &&
        (a.tagName === "INPUT" ||
          a.tagName === "TEXTAREA" ||
          a.isContentEditable)
      );
    };
    const guardShadowInput = (e) => {
      if (!binding && isShadowInputFocused()) e.stopImmediatePropagation();
    };
    ["keydown", "keyup", "keypress"].forEach((ev) => {
      window.addEventListener(ev, guardShadowInput, true);
      document.addEventListener(ev, guardShadowInput, true);
    });
    window.addEventListener("keydown", onKey, true);
    const inGameNotify = h(
      "div",
      { class: "ptp-notify", title: "Click to dismiss" },
      h("span", { class: "dot" }),
      h("span", { class: "msg" }),
    );
    const notifyMsgEl = inGameNotify.querySelector(".msg");
    let notifyHideTimer = 0,
      lastNotifiedMsg = "";
    const hideInGameNotice = () => {
      clearTimeout(notifyHideTimer);
      inGameNotify.classList.remove("on");
    };
    const showInGameNotice = (msg, force) => {
      if (!msg || (!force && msg === lastNotifiedMsg)) return;
      lastNotifiedMsg = msg;
      notifyMsgEl.textContent = msg;
      inGameNotify.classList.add("on");
      clearTimeout(notifyHideTimer);
      notifyHideTimer = setTimeout(hideInGameNotice, 9000);
    };
    notifyInGame = showInGameNotice;
    inGameNotify.addEventListener("click", hideInGameNotice);
    shadow.append(inGameNotify);
    const checkPackStatus = () => {
      if (!HOST_IPC_TOKEN) return;
      try {
        fetch("http://127.0.0.1:47921/ptpack-status", {
          cache: "no-store",
          headers: { "X-PTPack-Token": HOST_IPC_TOKEN },
        })
          .then((r) => (r.ok ? r.json() : null))
          .then((d) => {
            if (d && d.pendingUpdate && d.message) showInGameNotice(d.message);
          })
          .catch(() => {});
      } catch (e) {}
    };
    const statusBootTimer = setTimeout(checkPackStatus, 4000);
    const statusPollTimer = setInterval(checkPackStatus, 15000);
    (document.body || document.documentElement).append(host);
    reconcilePackKeybindsWithGame(true);
    paintPreset();
    uiReady = true;
    window.__postfxUI = {
      open,
      close,
      toggle,
      persist,
      shadow,
      destroy() {
        close();
        clearTimeout(statusBootTimer);
        clearInterval(statusPollTimer);
        hideInGameNotice();
        window.removeEventListener("keydown", onKey, true);
        ["keydown", "keyup", "keypress"].forEach((ev) => {
          window.removeEventListener(ev, guardShadowInput, true);
          document.removeEventListener(ev, guardShadowInput, true);
        });
        host.remove();
        delete window.__postfxUI;
      },
    };
  }

  // Interface tweaks (plain page elements, nothing to do with the rendering)



  const HUD = {
    center: false, // move the supplies bar (health, armor, damage, nitro, mines) from the bottom left to the bottom centre
    scale: 1, // size of the supplies bar (1 = as the game draws it)
    classic: false, // draw the supplies bar in its older look (see CLASSIC SUPPLY BAR)
    classicIcons: false, // replace the supply indicator icons below tank health bars independently of classic bar
    beam: true, // classic bar: the beam of light when a supply is ready again
    effectTime: 45, // classic bar: seconds an armor / damage / nitro effect lasts (0 = do not show effects)
  };
  const CLASSIC_INDICATORS = {
    health:
      "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAG4SURBVDhPlZDfK0NhHMafnSPHq1xsZ2fbe9ii/Al+3YgLNrIVIj+HFCXxR/A/GBcujCul5NcFJXbYtM2FUQ4hN5N6c+VKKXpnmMOYp56Lt77P5316gL/lAzADIJDhnOQEsAYTWKGvnMkzbiYHPOw/gPU8ZxGzr3cwF5t84SaNpTkD6kz5IrNvvIVLLkcfpCrKw3cAGgHMZvhHuQvqXR8/0/3e9/A0gCRpKE3Ks54kgKAx+C43cZd9AFz3E8/m6doURKpRmTM5/qQEvfydFbArVTg+AWk7dnuY83bsMWOPHwFRwUKi/Fg9GmD892+g7S6GPIED+B5fFBdkEldjg1fWuaaoqBTeFyeGvzSxrbYzsbiIh7eN4YNU+HjoxjrfHOdNlIWWhGOnm0nVKuOb8GHTY64awzFBJjo96Net8806b6Is+SI07D8XLCQMYIcPm3adMbwsyCSmxgevSy5GmClf1JWgN0Ijfl2QSQQAB/wqzbbcGlMWWnTzVK1Ow36d7veeiNZUmPtPaVTrO5MDHs220hamob7TdO0942E2aVIl1exbnYfKojckWEgIAHfOqgSwmeFF48FvegV8acjXSZNNoAAAAABJRU5ErkJggg==",
    armor:
      "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAJTSURBVDhPfZNtSFNxFMbPxqzmXUw3QwebY93hhtvc3UrSWVh9KAlr1QfFMo0GZaxMqKBYZW+IYKKY5FxvEhIhBL1YFH4sSookwg+NBHWN1eUIguIbIsa5uu16Ex84357nd+45/3MBkioDgJsAMAgAsTWqFwDOAEC+KAtP1ilkeMyjweZyPcZaHPzCQ/fCbMg1OxXkpuYfuOYXH7kXP17OwYslmegxMwgAv8WA7y/OskimyQ5usue0iX96ysQHSrP4k8UZf9qOGLC3jsX+KxbBM9PJzSx/TRIQuWPHaLMD3cZUPMCp8bl/M76sZfH1ORbfnzejPj3lHYWqi7QC5D/ASJN95MtV64+anRmCQVyPfUbckCLvAYByp0G5KmAg3GALf6u3Dhwt0KwAtFboaV4y91UWavoOb0lD2o8U0N/lM0Z+NuRG8gxKnAu55ijckgzHntWY8Os1KxZbVPzEPeeEFHC3yqPB6SA37XWphQ7tlYZEuLM6W/gqglRsSx8eb3eOA8CwGFBWwDKCqd6rw0KW4ePhYNVSmOrGQR2+qWNHB2/ljgLAkBiwVS4D/BSw4K9Gm9BZIZdhaLkz1VCjDUudaqS78O/eRIAPYgDpLRnIfN2rwx05qkT4b6sDD7nT8HPAEh1ryxvTqhQEOCEF7KHOtDgKXdqXhceLtEi7IFiXz4h0mR4zEwWAsDQc12066Ve1S1dJF5hvSk1c4H5OTeMRYMV/IFU3s16OHaLl0dy7rBvjr7JXGlhNTWS+UJKJ9HRctpLCNPd2qXEt+QHgvqjsUgPpH2KnRqaa2mOGAAAAAElFTkSuQmCC",
    power:
      "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAJCSURBVDhPlZJdSFNhGMf/Z66die5sbcOdzann7KztbE40ksyxlCS/chAl1AjEqKsodxGR0eXAEMELI7zsIqibiCK6yYvYtJsOdDFzF4pSeOFHj6KIeCNqvOLguBbhH3537/N73ud9H+DkqQXQo+NEGQAwWy+YZ6+LwmyhQAVwToc+fgBvASw9Vhy01hFcaxDMfwkuRyz8WJOtdAwAo/eIJIC5YLmJJppq6CAePnjocxC7CYCIXtBxpaI897LekwOQu1VpTbt4Y5oDck/8zrn1zuA6K/7SXMOKlwDcyxdeyxN3WVaX2wOrDYKZsi0+uuERKN0s0Up7YOVn2xna7la3/WUmJniv75xKeISUYDSkWMFeT3hvozO4wbox3jV6qc9rXdzpDu0MKs5FAL8AyMcEo2HX6N1q25tBxXk44348vP/9oo/uVNnIwIG0mEyTUYl1ZgL2E8eS6vfaUo8UR/rT+Wr60apQUraTycD9BrDw+mzlobTPayWP2fgRQGOh4Kb9VMn9iIWfyrb4sgOSfQrAJIBnIm9c3uxSN7e61K1MVKILp0uLCljYDmgzrcpMv9emAXjFXvp5RKTdntDu16hEIm9kI7AfegogUChg0ebb/PMfGqu0B5I9M1YrHl49z0p7gIbUilyonC8u4ABtvM6taTFZm25Vpr/FZBqvcxNbmttVNkp4BMa/BQBGAEwAyDAuOcoyLyLi56RsH+ENHCtK6HamqECfWGkJN9zrFoaH1Irhq6KFCboLD/0v8aMHy1NU8AdMAMlp98wUCwAAAABJRU5ErkJggg==",
    nitro:
      "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAHhSURBVDhPY2DADloZGBjOIGF/dAW4QAgDA8MxEUGm1xMr+V7f2yn6Osyd4zUDA8MzBgaGpQwMDNroGmAAJLEcpDDah/P19c0ir/9fkfgPw5unCr7WV2cBGXSPgYGhHF1zDQMDwwOQgq3TBFE0IuOPJ8Q/NmbzvAa5joGB4TgDA0MAzIBn7QW8r7+cFv8CUvjzvPjPmnSe10cWC4EN2zRF8PUWJINvbBF5He7B8ZqFmeE5AwODAtgAmOSfSxJ/DiwQAtnwdHm3AFhTcQL3a3M9VjD732WJfyD690WJ38qyzCADFMEGfD0j/jUlmPP15fUir48tFX7BwMCwc91EiAEg11gbsoHZIC9MreF7/eGE+AcVOYQBD9UUWB6C6BtbRG6cXC58joGB4SCyAbbGEAPi/DhBrrurIsd8l5WF4S7MAGUQ5mRnPPdgl+iDS+tFLiEbUJ3G89rOBGJAbjQXyIAUqEawZmRwLjmI89zSTgGwC9ZDDajLhLjg3Grh12oK4KgEGYAVRDMwMMxgZmKYwcDAsHjjZEjI95bygkJ8HysLwz4GBjAGuZgggBvQV8YLsrUZXQEhsBzNgE50BYTA8m3TIQaAoo0sA5Z2Crx+e1TsbWY4OORJNgAUkLeQcCK6AhgAAAe6DueC6+jOAAAAAElFTkSuQmCC",
  };
  if (typeof window.fetch === "function" && !window.__ptClassicFetchHooked) {
    window.__ptClassicFetchHooked = true;
    const origFetch = window.fetch;
    window.fetch = function (input, init) {
      try {
        if (HUD.classicIcons) {
          const url =
            typeof input === "string" ? input : (input && input.url) || "";
          const m = url.match(
            /\/assets\/battle\/indicators\/inventory\/(health|armor|power|nitro)\.png(?:\?.*)?$/i,
          );
          if (m && CLASSIC_INDICATORS[m[1].toLowerCase()]) {
            return origFetch.call(this, CLASSIC_INDICATORS[m[1].toLowerCase()]);
          }
        }
      } catch (e) {}
      return origFetch.apply(this, arguments);
    };
  }
  // ======================= CLASSIC SUPPLY BAR =======================
  // The older supply HUD (pixel art slots, flashes, the "ready" beam), ported from the game's previous interface, drawn on a canvas on top of the
  // game's own supplies bar. The game's bar stays where it is (invisible) and is only read from: which supplies are in stock, the cooldown
  // pie and the count text. Nothing is sent to the game and no key is handled, so using supplies works exactly as before.
  const SUPPLY_LIB = (function () {
    const self = {}; // stands in for `window` inside the library below
    /*! supply-hud.js - self-contained supply HUD (5 supplies + gold slot). Assets, logic, renderer in one file.
     *  Ported from battles.swf by static analysis. Usage: see bottom of file / README. */
    /* Generated from battles.swf by static extraction (no SWF code executed). */
    self.SUPPLY_HUD_ASSETS = {
      bgd: "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAAChUlEQVR4nO2Y366iMBDGadGICfKOe7XRN/Ju388bgggILT35WKZnqK2YYPa4CV9CmLbD+GOmf4hRtGqZBG+cz+c/bdtqIcTQb4wxg9PYhuI4jrTWEW8rpQa/zWYjaAz9ENqIQzHoeV9s9MVxLI/H428hRDsBBBzuAMS97/shgCsppaAxKeXwPNpkk3ifzw6NAxD30+n0a3hpCljXdceDu0AUQGttx3TAnhuHjZhILMWmca11z2NYQKXUd92mmaD0CyrLnIQQ1pfZcjSNG5vuPnFA1wsZ40CTEjpjrgTzIVs548ax+TOPgG3b9qESP5tnkeP3bnFAW2LfCqN+d9WJsW5zvlJKbyndGONKFkFAF47PGRcuCvT5Xs6FCflAXWfX6zdg0zR8jkwAQpmLnmTsXXoARBlQDhKVhff5/Ei+51/xmwW83W7ebcZXumflFZ4t5llm53z5Rm0zyCc1vaXbTzYX+bvj7uLgsX2+PO4kg3iJSaRxg6aXoz6fH/flbbJ9/W5cn29wkeDowXKnIwh2Xdd9VVWqqqq+rmuN636/913XGf7BsN1uxW63k/v9PsaVpmmcJAns4KTjv8P7eQYngAy8L4pCFUWhy7JUBBKSUmqAxcvkeW5jAjxN002WZXGWZZskSUKwkxOMZ3BykpRlqV+FekWIked5h8uFdb9w+AllAQGEQZTter0CDKt6MdgMrErTVB0Oh2Eq+I5TC4i5BLCmafQ7MvaiUCmF38QcBajrYAEvl0vn+aL5J1JKEWiPuYkp8AD4U3BcYGiaxmAXeAD8FKm/u4Btzx+GPyPz6YBWK+BSrRlcqjWDS7VmcKnWDC7VmsG3ZdAYM/zD+gkyH8QS/ff6Avvrgiz4/aJCAAAAAElFTkSuQmCC",
      border:
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAABBElEQVR4nNXZsU3DQBiG4ddfkVUQEiOkYgCIqJmDCaChyCiMkIoB0nkKSz5Lli6+4kcnjqDQ0eCPpzqdm1e/dM3vjiYitsADcAtcAxv+1gL0wAF467ru/fwlIh4jog8ffW06T65dnFJKz6WUe1ZSSrmrDbWlNW1r4L4mp5ReMDGO42ub5L4GHutpzcn9VFta4JE2zsBMfDpphdf6G5saaE2YE+aEOWFOmBPmhDlhTpgT5oQ5YU6YE+aEOWFOmBPmhDlhTpgT5oQ5YU6YE/8gcMHXorZ2Jee8w0T+bunVdsLM83yFiWmabtrxcLECHobhac1J5px3teFiBey+RO++6l1/Q3wA7hrPb8n1XlwAAAAASUVORK5CYII=",
      dark: "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAAAyElEQVR4nNXZMQrCQBRF0euLGwiYVkiZxiW4eJdgYxmwVdANBGTgB2sbvZ5ypnnMzG/ebHjbAxMwAgPQ8V0LcANm4AJc2+KmNg/AEdjhcAdOwHlbJ9fC9Xj0lem5rWu1nNyqq0xT6s1ZjamBsBryg2n9RNcCqgW5IBfkglyQC3JBLsgFuSAX5IJckAtyQS7IBbkgF+SCXJALckEuyAW58AcBF7yWVO1qdUt1wlZzqrC+y656qUyXVJveCusHHo/KdF1b/kb5DfEChbIgIQiItHAAAAAASUVORK5CYII=",
      lock: "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAAApklEQVR4nNXZMQoCMRgF4XHYA9jZLwje/zSCsL2VHmALCURs7Ry/MmkeSf7m5cDHCViBI7DwGzvwBDbgPhYOc+MCnGm5AddlnlwtHDPTw3mtVavzzVUd/eFAfGMZAdMkTuIkTuIkTuIkTuIkTuIkTuIkTuIkTuIkTuIkTuIkTuIkTuIkTuIkTv4g4E7X7uyEq57Owrpqc7bpo7CuuY1s75Z/SH5DvADjVhUoJ3RTyQAAAABJRU5ErkJggg==",
      firstAidBgd:
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAAA1UlEQVR4nNXZsVEDQRAF0Z5fZKAY5CkC5B4hYJMFxCCywCaFcyECeReDUmCoKYGPp+ZF0HW76/wrrup5ZaniWHAAdtzGpeHczcfrwgp0TdzLylPBPcUeg2Zr+DwtvM2Xewg8auJ+NdsXvN/NsYIsbhT7gmN+7pxSwSE3fBB/sZtAtSAX5IJckAtyQS7IBbkgF+SCXJALckEuyAW5IBfkglyQC3JBLsgFuSAX5IJc+AeBF7wumU0YqYZzZrCeuRWbZpu2zJo+g7Uqsq8j+rTNyj+0vyG+AZo8Oxec4HIqAAAAAElFTkSuQmCC",
      firstAidWhiteIcon:
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAAB40lEQVR4nO2WwUsVURSHz0wFKqmIi0x7/hOJC3Olf0KuxNSFm3YtWwjSSqNFLd0FFerGB2pGidCmSHGRLxNsbWFiBkUQLTqfXLxj0zD63hNn7gjzwWzOzDDf/O49Z0YkJycnJycRgALwANgBtoExyQLAZeCuqn7hHz+AXtdiHjAAbPE/34AucY2q3omIBXLdrt0EaAY+h81UdTcTcgagLya9+3ICwBXgtpTBl7PhZozAz+MuBppEZFZEJtISvBgteJ7XDVyIkWsEiiJyQ9ICWCCeJ2FJoB5YDu3Tt1W/+WkdJZ5bgA/Mm1BFZEREjuah53mjkjSq2gC8pHrmzOxM46uxZJfrNbBZodwfoCON5F5ZuadW+LqqlkypjODjNJIL5J5FGqFYRs7c15qkXJ2qvrAPmwKOGg0YsvUVYA1YBd4Bb4BJoMc0TdLJLVqJ6YjcgE30PdCSmMQJcnXAcys3A1wKneu39XXgqgu5WjPLgg0eSa7fJldyJVdvZ5bhLzAclQM+AG2py1mJ8ZhOHAQehuSS68oKBL8Cv82Ms8eeHbSGDWfJBZgfT1XdDwkHmK9GwamcFboXs8QfgXbJAkAN8EhVvwO/zKhR1WuuvXKq49Ngp2QUP8tyh2Rc0JeM47sWkPPOAYD9Fz4r5v6+AAAAAElFTkSuQmCC",
      firstAidColorIcon:
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAAC8klEQVR4nO2VTUhUURTHf+eNH42aFQZB0UJTpHZBtclFEpkZhRFMaVougggqWlcEQ4vaRbsCQcMs55UtgiBaGUQQGZSNIxWjVvRBIIHQ1zTeE+9lNITmm3yjRvNfvXvevff83v+eex5klVVWWf3fkkxtfOU5yyWHI5bSpEJSldbdZYTT3SfHbzA7ShGFHFI4LMJS1wJlVA13Z9tBicTZIxYnECp/BlUZMUm2N1Rwb1YBu4Y4aglnU2MOnBjqQyv+zj3fAC/GKAkGeYywLAXuvRh2TgfOkeUHYDCf6lS4cbX/Ce5ynCX2EAdnBFCFnb/HBEYnm9/Zx6KARbfCmRkBFJmgGyhVtk3g97AdZ0FOEddFWO9lb38chHkT7FzLWtpSIVsHmK8W3WKxYTwUnak+qBNGhWZdixUZ5IaAqLBfhI0pi05k/BZ3PKM4LxdbhM1pLVRuhEqpn/Tj/DhiO0pRfi7XHDg19KDEPMIlkmOcmgpuWoCOc1pIN8Imo1zaVUY1Y+xVpU91isTK5cZyer3ksf7WubxcropQo0qn9YAWJx4q56EocZHJS0eV28mvHPeaK+1LcqGXAi1wa67GGK4MvKAlHGLMeRcZZB8WO1S5DwQEjCoGcd9HZQw71kFPOIzxmk/SdU4LiIhFnRq6Yi9oDleTdOHiNEmADgyPELaESnmX7sdPC9BxblEJNsJWlMiHEZoPrOGbCz5EI0KnKo8Tn9nSvIq3+CRPNWjfI7iwhC4XztDeP0zTBHB9fsN5qkGn+zOPSyJsc+pJhDs/j9Ueh0N5Ignq/IbzBFiczzGE7c6ziOt4W9cgRmA1wlEHLvmF2saVvPEbzs051YTIEO9QFoihyhlrgFsCxQh5qkQlQW2oktdkSF7ajNPVPjk9zhnYwyx2o0rMJKlryCCcJ0CB8wgn7eFffwdV+sVQ11DBKzKsKW/xRziNck6VD6p8RLmZ+EZtaAUvMw2XlX96uncdc1TWXIb7oTkOaDHHZc02AP+6vgOeNxQQL2f7QQAAAABJRU5ErkJggg==",
      firstAidGrayIcon:
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAADGUlEQVR4nO2WTUgUYRjHn+eddW1DC3EgKDq31y5d8lBEZUZSiJf8yKCElqw8iGuOu7TLbC0K0S0oUOjbsoMQRCdniSAoyL6kQweLPgwiEHI1Zt5/vDTJJpqjzabR/mGY5T8zz/vj/84zzxIVVFBBBf3f4nwV7uzsXK9p2jEiqicim4guJhKJUwutE/AbLBKJlJSXlx9l5hZmXqs8AOMA7i91gmwYRp0QwmDmDT9NAJ8dx6k2TfPBYor6lmBXV9dxIcTZXM+F27tYON8SjEaj5cFgcJiZ1+XAfXIcp8Y0zUVt7U8JPwADgcDWXDhXfb+D6+joWBOLxY78FUAhRM0s9vhc90ej0bKioqIBIjozb23yR4FZvIra2lptptne3r46GAzeZubNXgr7BbhipsHMleFwuDcXsq2trbS4uHiAmbe41vO/1cWYzRRCNITDYWEYxqAQQjXkIWbeNv0QYOQdsKWlZRURBee6LoSoIyJ1/CIAg8lkciivWxyJRErKyspuMfNOAEMAXnp5DsA327aTcyXvC6BKTtd19T5tB3A5kUhsBdAI4CmA+Ra+mkqlHnlZR/xBcjeZeYeU8srIyEiT8pPJ5GMies3Mcw4AAPey2Wyn17UW/A42Nzev1HW934W7lslkmizLctS1WCx2gJn3AXhIRKp7pXs4AJ5LKftN0xxyPf9HnUpO1/UbzFwF4LplWQ2WZam/UmQYRr2maZcAPJmYmNjV09PzkXxQYIHJXVdwUsobY2NjjTlw+124Ydu2q/yC8wzY2toaKi0tVXC7AfRlMpnDM+CuqOawbXtXKpX64BecJ0D19Q+FQpeZeQ8AKaW0ZoF7ppLzG84TYCgUOsnM1eo3MwtN03oNw5BCiI3MfELBZbPZyu7u7vd+w3kCJKKDACYBVLiQd4UQF5g5qDpTbWu+4LwCqk6fcL9xFI/HdXVWU8NxHLWt7/IF5xXwPDPH4vH49HQA8GJqaqoqnU6/pTxr3kkyOjp6Wkp5DsAXAF8B3JmcnKxMp9Nv8g1XkH961biJlqnEcob7oWUOKGiZSyw1AP3r+g6e327GbBd6cgAAAABJRU5ErkJggg==",
      armorBgd:
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAAA+ElEQVR4nM3ZMUrDMRiG8SdvIgGXItTNA7h2sydwE7yJPYneROjmCXRz9QBu/qF0KQZTIx92cHTr+zvBA0mWNwlgQBrrxd3o82Vvs/l+f5o4gpx3o9TtlMr0nG5eHxKMFHE8Xt1/9fPL3s4qBkrdtJPy8cbtyyp9rxer3i6uXeL+Rpb6/lR+j9UrLkRTzp9LxZ3DVG+zuY71IP4j2oQ5YU6YE+aEOWFOmBPmhDlhTpgT5oQ5YU6YE+aEOWFOmBPmhDlhTpgT5oQ5YU6YE+aEOcXsiqmcd0OxCWOq1O2kGKxjbsVMqZsWbYo1PQZrp8hyGNEPS7/3N8QP/YpkauW21+IAAAAASUVORK5CYII=",
      armorWhiteIcon:
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAAC7UlEQVR4nO2XTahWVRSG3/dcifyjQkwxrTTJNCEVxPwZVYMgsImjhEaVA5U7sFFOHBk4EgwSfwaiqIXgoFlEFETOGvTjtVumzrIGcTX/r/uJfVsHtofP+K6em0HnhfPtzVprf/vd71l77X2kDh06dPh/w+MdACyVtETSU5KeBRbaniXpMeAhSaO2R4DfJZ2TNGT7gqQfJX1j+3brqwCeA3YBX6eURrl3fAt8CKxpVUHgM0kvN2wXbP8q6Txw2vYvkn6T9Cgw3/ZiSQslzZY0T1JWt8aw7UX9zD2pT4ID9thafgA+tn2yqqrvGjFTJc2QNFJV1Uhhf1jS08CrtjdIWiup79fcF0HbKbpHq6raCcwE3pK0QtKTkuZLmitpiqTrkX8/A4O2hySdyQ9wMQjSKsECA/kH+NT2srvETMsPsMf2cMOX1RwXxktwDLYn/4MbSe9UVXVALaAaZ3z9am71dELOrbdt30EOeA+YfLdx900QqOOmR3ulR9hN2xttH2yM/QB4N+dmsZPdKkHbV6Nbl4ac9CVuSMrkPiqIGdgtabPt87az+o+He7RVgpLORrs8lxxJnxS+m0HuRGPMPkmD0f8q2pXR/qk2AbyRj4E4RXJNmxUnwxVgQyM2K7e/PD5SSi/mepjLT5j2tk3w+TxP/PmYKsABYH0jbqBJDjibz2hgbWF7vW2CefXfhxrngCk9YgZSSkd6nL9bw3+yUP2JVgnGBNuKSd/sk9yZWNyy+g2klA63Ti5IzEkpXYxJ8gXhkbDny8GxHuTykfdKxHxe53DOxwkh2EPF/WF7gd7YHP7BwnZowsjFZJOAL4oJB8O+Hrhc2LeF/bVQsla9roMTSjKXmNM1k5TS9rCvA07V+ZnLT2yIHPMHsGrCyRUknwGGC8V2Nfybipv3JeCOy+6/RXJBqSRwPGzvF+pm5V7Sg0JKaS7wZUHyWtH/CVitBw1gKrCjsYP3Afmr778D/t4UQ8CWuPt16NChQwfdP/4CR1BPJFf/NV4AAAAASUVORK5CYII=",
      armorColorIcon:
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAAEWklEQVR4nO2XbYhUVRjH//8z47ZuSkaZQTozq2YZClaYH5Sglw9ioURrGNvMOMOaHwoMtAjpi0VBGWXkErbt3tmZtHR7AROE6IWgl41EMklT3JdZKlI/pNhus7vO+ce9d1Zv6oeZdfzk/OFyz33O85znd55zz7kzQE011VTT1S1WGjCQTc8TdQfBqIQ5BGaDmAbgegF1BM4COA3gpKA+AIcF5A10pHv34P7HurqKVQfsc5K3m5BJk1gC8B6SIYxDVjpI6Dsr5WKJzPflxITLcQqFzFYa80DQJtk8wL8A9AM6ZIFegicATYHYSGAuiNkAbgYwg2SdIecDnG+g+wHcVjVAwK+YpF8B7cKoPo2kMweDHj9nn7h2sngDCoXTs9Z2uUvsqc9J1gOImbBZSrCJ5GIAZS9zeYCE9QCBHdG488rR9tTUgdzqFsDcBSkCsBHAdBg0oGFCYSCXPgngWNGOrmtMdh4G8Jt7DeRSxwEuhtyhqgl4jlNeJevr+DlpFvjGi17jSe5lrX37pz2Fo//rEeor3ZYVAZ5PpImXAPMkSYB9MprIvIcqqDJA0VsaEaOXwpNUhLQ2ksi0B+35XHojT5x50xKjlW5/Ux6Y7ydisnsnOHgJuBFZ2xxJOBfCbSWwIbK+q2DAOs/I8he6PEBiyL+VjgZ5L30QbthCzdFkZmcwaiCb2mLIpwD1e27QTX64d5hXD1BST6l5566VK0MW+CzQN2KF5ljc+SgYk8+m36Ux63wffOtPkAv9u/6pKiCl7lLzloXLGmYUh4e8hJKG3MrFEh0fB93z2XSbMWwZM1jaHaXz8F5/VvylqoCSPeDuTvcTZ8KhFbPWfHjcSu0CHg9Wzq2uW7kgnKTewWND+wncTfLG0nh7qwpoRXeJD7ltAs/s2/ZwQzTe0RKNd+wOwi1aPqkzCFfSlnmbukYYNhtKwEOU3VdVwMZUZwFWjgdIxqZOnNoU7N81BkfTHLTL2iPFs8W2Pie5gOAK34hPIquzf1QV0Bt3lB9IOuFD4sWebSuvc9t9TnLKouWT3r8IThqW8LQ7uVDYvEGS7jlZZLG13JwVAUZbOv6E1Ws+oImGJ05+3QMJmaihWXXxjLQ+mnS+yOfS60hzX8m6vTHe2X1FAF319Pa+ZaVvvEDDFjf5zIRzwEorpPNHh4p2QyThtPbn0g8ReNXntfkRjTyLK/2Luqdt1bQJ9Q1fk5zrPlvZF6Jx5+V8Z2oJyc2C3okmnGx/LtVkwE6SDZJOnZWWzkw4P15xQFf5TGIWQ+G9JG/1IK3dHE04z53rz61eS5hW91iS7BkLPhKLd3w5nlzjAvQg2lMzWcc9Y5WUtFMj2sgJXEPD50u2U1b20Vgi89V484wb0NXRXPP0a1C33dB4XwhJBZL1pf8fx6xFojHZ8cPl5Khok1yoOfHtv/+t0WVWdpP7PAYn2bbiMB68XDhvTFRJ/f6meAmyrTg52B5Z3/Vvtcauqaaaarqa9R8ld+kR9u3GKQAAAABJRU5ErkJggg==",
      armorGrayIcon:
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAAETUlEQVR4nO2WXWhcRRTH//+7N7uxGqyoUWKbtPErEQt+oC+K4MeDHw9BbKgSMCAxfVCIkJpkk90L7r27IYlxI5oXY5CgrbRRCyoURIWAH32QQhWbxNSPEBVrXxR3kzS7M0dmuSlLSCCb3j51/3DZmXNm7vntmZlzByirrLLKurTFUidEo9HbQ6HQbZZl1QG4BcBNAK4DcBWAMIA8gH8BnBWRX0lOa63nSc7OzMycmJycVIEDRqPRBtu2nyN5P4B7SYawBYnIDwC+Vkq9m0wmv9nMHHszgyoqKt4k+fCaYPMA/hKR3wCcEpFfRORvktsty9otIo0kTXavB7CTZJjkHgB7QqHQQwBuDQwQQCFjIvKjiBwRkaOe55lsnFdnZ+fltm1fbZZ3YGDALHFBra2tlTU1Nbts236U5F6S9wHY9DJvFlD7gIdc101Fo9FrY7FYG8m7SNYC2A1gB4BtAJYdxzkL4LTWusPzvGkAM+ZxHOcMAAMoQQMWtLr3KioqPiN5xwbDrjCPiLwxOzv70xpfZSnxSgYs0mUbOUREtNbtnue9jQBUEqAJ7jdzG/gVgP2e540X2+PxeG8mk0lrrXOhUGkFwCplHMkqv59dB24FQEsikVgLZyrAgXQ6vezXycKrggZc9H8LpUFEZtbAndNaG7jDRWbG4/ERy7JeAGBKkZCs9n35QAFF5Ge/eWdzc7NZo0+KfCsi0uJ53gfFcxzHecuyrA5/zFcFYvIe350JGvC437yhvr5+Zz6fLwQUkUWTOdd1PywaTsdxxki2rRqUUodMPQTwgD/v+0ABAZw0B8SUmUgk0tTf339Gaz2utX6mOHMmuyZzxXDmCzM3N3eitrb2bpLX+OZjgQIuLCyYJT7ld19qb2/f5rpum+d5HxfDNTQ0TBTD+RqZnJxcMQfFB15USn0XKODExIQ5ge+YNsld1dXVe4v9zT6cZVktxXYRmZ2fnx/r7e01Rb3JN3+USqX+CBTQaGlp6X1zGShMsqxEd3f3labd0dGxvbGx8b114M4ppV40f8627ddI0tTJfD4/utmYJQEODQ39CWDQtEnWRSKRV027qqqqjuTT60zpTCaTnzuO00HyQd92MJVKHb8ogEZTU1Ovi8iUD9lmgnued1Ip1SQi50uHiBxIJBKj8Xj8CQADvm0+m82+XEq8rQDmc7ncPhGZ9iFHHMfpM4dFKfWYKUdKqdZEIjEci8XM1eoIyYiI/CMi+4aHhwtb5KJe+Y36+vpuDIVCx0jebPpa6yHXdbtW/Y7j7AcwasqSiPwnIk+6rvvFVmJtCdCop6enPhwOf0qy0Yc8nMvlesPh8PMke4zNZE4p9VQymfxyq3G2DGjU1dW1o7Ky8iDJ1S/EMsnCnU9ETovIs67rfnshMUrag2s1ODj4ezabfVxr/Yrpr8JprceUUo9cKFzhnQhIsVhsr2VZroiMZjKZ8XQ6vRTUu8sqq6yyLmX9D/8d9nTBFYQiAAAAAElFTkSuQmCC",
      damageBgd:
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAAA10lEQVR4nM3ZsVECURhF4fMf6MAIcjMqkIzROqyNOnDMsAIyYjWiBHjOv0hOxv2iF97ZfZucLYABxWbxysw1jNWAJx6g4AR14HzZ8/n7UTBqGve2fAdeBjwToOAIfLH72c6nJxc0rvWW6sNm8W2/1qRxN9OmmWv7zhFrrHzUB3GP3ibhJJyEk3ASTsJJOAkn4SSchJNwEk7CSTgJJ+EknISTcBJOwkk4CSfhJJyEk3ASzmt2zVRwcmrCsepgB+v/JhyletP5srdregfrpJF1i+jX0p/9G+IPino9uHYKMFoAAAAASUVORK5CYII=",
      damageWhiteIcon:
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAACnUlEQVR4nO2XT4iOURTGn+PPhMhCwmgiIYrESM1CRLKiWJAU8m9DFmIrysZGstLUlDCiLDQ2s7AYJRsbSaGMyNAwNDOZGYNxfro5k9c0n+bP96K8T32999zv3Ps83733Oe/9pAIFChQo8FcALAAO6l8EsAt46e5r8pjcgOoRjq109yv8wKmyi8sQHRvuGHff6e6vQ9xdYExe4mYDVwfptxL5VUB9CMPde4AluYgLwiXAG2Bqpq+mRO42oJVfcTQ3cUFaHUR7M33XBuTMBOoi73MyRLSbgLF5C1wfZI0RrwQ+Zc8a0Bw5D939RFpxd29398V5CFo4IN4R5F+iltVGvDHj0ITraSzwIM7egbKLC0GHU93KxPsyIuqBjszhT88+4Hjk9otvKGWicgisAbqARREfGnDo37v7i2g/AdZG3v7oawPm5iIuiMYAT4HtEZ8EvoaTifgWcBOYFjnL+1cU2KO84e6XgSNBfiatGnAe6ASmA6v6txCYnMwR4m6MlnvcUJLM7LakqggnSOqTlErLJzNrk9QW4iok1UlaKqlL0oXsPKnEmNk3lRvAHOBstGvd/W2JvNXAHaAl+SWc3hvbv3kkNXBI78P0NpCUysV4YJKZ9ZZIbZHUmMRJSmPSFm+VtMXMGnJZvYzIyngmQzwd5PvTQDc/cQ6YoT+NuJE8HkTgOuCiu3/IiExmugRsAiaOhG/YxTOIO8ysOUyWiNPWp7kqzKyyxLhXYbb045rM7H5uAuP5xsweJbcCPWbWI6n/805Su6Qvkj5K6gxXt5pZa25u/huw0QwGZkmaJ2mFpGeSumO1nptZWjn9kUL9G3xIvpE0RdIGScvSfdHMkjl2m9m90Qos+w0DmC8p/VubI+mqmT0pN0eBAgUK/E/4Dkd1zykM0xbqAAAAAElFTkSuQmCC",
      damageColorIcon:
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAADuUlEQVR4nO2XXYiUVRjHf/8zH+6akCKVWiJJH7bqTFkQ1UUfSF1FUK1BQobpzOxNEpRXEQUVQVdFxa6maUiUFhGReSGxXijUUjQ6my1t0Ze2VGairunMvP94Z7ZWc8GZdUWI+cPL4Zz3PO/z4+F5nnNeaKmlllpq6bzI+WuudFc2d6Z9gfMg5zMPofR2V6oDEw5okLsWXj8usOVXz4oKmU2EsBHYENaWdkw4oGLGSLc3axfls0udbuuTwlIc7aS7+EwjdslmHTmfvRSx6LR1UA3+v+sPz5/NpOQLBD1Ym9vHBAVB1Ii/5nNQngbc5tzcC/+FyC24aUy4fHYJbcm+f+Bq5pGfVPfuUqPuxgM4CWkmYcp9o19JrDoFbFnHDOez6wh6B5iG/WP9hXdwcPdLzbhrHrDK1LozLakNuQU3APecnGtMTu0kaDn2HtvPASk7+tPlSpe2UJ1QQK/IXHXKgrh4ZLwj7mWEkENqcyFzV1yhCtoEmoujzVR8v6R7axG3Vod1/XubgWsIkITurPWtUcLJ9UEpSD8NI5FE78cVaruKvVrdux8gyeNIWewP1VN8vVm4xgBd/RzpNecWzBsBaxu1Vpz8Fds/SGon8oDMYnUXX3Rh4QoUVmL/jnl0rCKaGMA1pU+BfRCydWAuwq5g/zIyf0VmD/YHDB+/RT3FXhcy15nw8sj7J9RT/J5x6ox9MO5XEf5MgUvqC24HHQLexV7GiaOvkr7gcrqLfXGU3NkxBbRxJKLvqae4YbxwDQHWIb0dwuzaxGpDVKhW3iaROKY3Bn8D4gd3dqSZnlyHtBD7CCHqPvk77iTRbBU3dpKcqPSSTtV7ndxuS2Ft/y4gfkY1PXEjaAaO9oFm4bDVhWx8YmyDaD0H9nzUDFzDgN5fHtKc1JfOkTJMlvhrzI0V/UzC2yx1CYaAXlzdRE/p43NXJPGmbYPHKR/frjWUZcVH3GmALmSeJakSITwvKU6HzZSrj6mntHW8cA0DxtL6gf01kPpFYayD/hPMFtt/1A20ilSi3/nsm85l7nbnZe3jAVSzBu661vGxJelbXEuR2HHKMTpKS5o1pp39U73YtJdqpVdr+/vOyXUrlhSm2p4pXAIdMR6WGCbyMGYYol9BBxEniHQYfEiRjlCuDGnjV0NxNfN/kc7G2Ll5M9GkudiLCB4k4iiKDlGufKf1A4cnAvDsfpoOhgNUqoPANxAWE8JTVqKXdNsXXjn/5vMewbHkRzquIJm8FTQHV9/SmtLXY25sqaWWWmqJRvQ3QjSGWYBwxJ4AAAAASUVORK5CYII=",
      damageGrayIcon:
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAAEAklEQVR4nO2XbWhbVRjHn+ec3miCkCkV12oVRaUgMpyCqB9UGOIX8YNkovMFRIPrB4fQ2DS5N9WbpGlJoWhVpOrYYAxZFRFx7kMZ3YcpW1EEBzZ2im/T4gu6Llrv7b3nL6feSBzB5mYtBckfDjfn5bnnd5/zPOecELXVVltttbUhMk3zGsuykquNE7QBMk3zESHENBFV1gOQLcu6sRWwVCrVbVnWPinlXiLak8/nj6wHIIjozrBGuVxuRzQanRVC7ABw1Lbt55qx6wg7USaTuZSItjbo4gD+X8pmsz1SyhFmflDXASwtLy8/SUSqmflCexDAhcx8x8DAQLzWZlnWLY3gTNPcLqWcrcEF9mapVDrR7HyhPdjR0XEeM3cZhnEfEe0OmncR0Ye1Mf39/ZtjsViRmR8D4AL4hpkvB3Bkbm7u+TDztRKDm1YMhdiun5lM5iZmvrc+1mKx2NEA7lMARSIyAPymlNo5NTXlh5lMx81/Kp1OXzsyMvJ5HcADzLwfwLJS6jpmTgkhnlBK3U1ED+sk0OMAHHBd14pEIgeYeQuApG3br4b1xqoeNAzjLr1v1TXFVr6M2RBCPMvMK55k5reDDPUBPGPb9v2GYfQHcO/atv1aWLimAInoIyHEy4ODg71B/fxaB/8d/B6Ar5k5CqDi+/4227bLpmk+rj0L4GfHcZ5qlERrApjP548R0SnDMLboOoCLAWioH4L6i0SkY+0d13VvKxaLM6Zp3iCEeEH3K6VSo6OjX1GLaiaL9X51HMAluqI9RUSnAbxJRI96nvcSM185PDw8q3n7+vouEELsDTz6VqFQ2NMqXLOAWvrc7KlbYk8p9YaUcqlUKv1ERLpQIpGIdHZ2vs7M1wOo+r7/Sv1LEomEDJvFTQH6vj8jhNgVLGmUmblYLH5ARLr8o97e3puJaDOAU0TULaU8mMvl9AocUkrtrlQq74WBaxpwcXFxIR6Pf5JMJo0gi/9sNE4p9Z2U8hAR7SSiBQAzAPYVCoX31y1JtCYmJhzHcaYnJyeXmTneCNCyrIKU8gQzDzNzj94HPc97ulAoHGwVLtRRVy6Xvw9+6ovC4tn9SqnDQojLiOgeZr5Ih4RhGA9ZlqUBp6rV6vT4+PhSWMBVT5KzNTQ0BH1sEdEXwQfqrDaCd0WYubuRHYBvAUwz82d66fP5vM76tb8saDHzJgBdRKRvJVUi+qNWAOjnj0qpX4nIZeYzSim9LVVd110YGxtb0NlM/xfxuRhnMpkuKeVVzLwVwEml1O/MfNpxnC/L5fKZtQA8pz9N8/Pzv3ied9L3/Xlm3ialzAkhZqLR6MfZbPbWDfdgI6XT6asjkcjtAK7wPG9/qVSaaziwrbbaaqstakZ/AbKFxT8HfY7bAAAAAElFTkSuQmCC",
      nitroBgd:
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAAA0ElEQVR4nM3ZsXECQRBE0T9dZEAM8vBVEi55EBt54KIr+efJxiQFlpoTGHh4/BdBV++u01sAA4qJHWELbAaseYOCCzBz5cQXx4JRS7hf9gy+R/GBQA3+KH745LBamhOFa52lm2PivOpjHXjCPYUstuk7h9cm73oQr+hs3aBakAtyQS7IBbkgF+SCXJALckEuyAW5IBfkglyQC3JBLsgFuSAX5IJckAtyQS7I5T67KhVcusEZrzk9WC+bsEx1piun9Jreg7UpZD1G9P+l3/0NcQOCcji5cJeqbQAAAABJRU5ErkJggg==",
      nitroWhiteIcon:
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAABTElEQVR4nO2VMW7CQBBF16ZIkRNwhJQRBWVOQckR0uQIdCko0+QUuQI1EhV1jpACTBQjNC9aa6ysLDss2SVGYp9kaT2e2f3+npGNSSQSiUQUROQOWNjLrs2lAAyAJ2DLD1uNDfoWN1bXurDPxn0IuwVmIvLJETRnZmv+RZyIPABLTmdpa8/t2lxEDo2D98ALUDixQmP7hpu2dh7dTe21dYsrX8BIczZOfKOxkeY0WUftzV8GoXByCo+4y8Ln7NxT472zfjfGTPXw3OPl6pyp1rbtGQZQ6lu/AkONVRN6zMF60jVnqHtYypgC34BJI1b14DGBdQ82aid2z2gCO0RX/EXgKfj2YBelR85NyAGhAg8eObu+BD4aY1b1jTvRjeleaW6/iPNfdqc7lNBPfHaSwFCSg6Fcj4NZluVt60ty8Bn4sJddR9w3kUgkrppvepVZizmJR/MAAAAASUVORK5CYII=",
      nitroColorIcon:
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAAB00lEQVR4nO2VPWsbQRCGnzkbQvyROqfKrUkVYnBhQ35ISBVIlyYY/BsknNZNcOc09g9wEVS4cGFI0qVNeZsqRRQ7ERY3Zk539p0ifCfpJBu0DyyM9mbefTV3uwsej8fjqYU4Yl0dpzYs5qGgRyxoxHt1dPQnmgyLbe6Ihfs159hMupYZGxz9jm5OsoaMaWwZ2FXYEeHxnbnKX4E9oCkhF1M3GEe8FGEPYWOkQuWLKjtBg9NRyoLK+o5ldXxAaBfMKVco+8S57lhsc/Ysw2qEtmmkb6C+DibfkXJAwLPiA7ooW9Lgqzp+I6ym8x0JeaIRLxDOEB4V6mK+I7yRkPO6Otj6z1zfSM/MpfGtVhonz5TekFVNq1Vl4aoGn+cW/0HM6ySU8vqbHKux2mGaNRjsvyLlI122pcEhIyJW02U70chrlrBYUf8E5ZOEHBcWpfwP5nNkDQe8Vcdn4NXUzkHDDmJM4GlfQyP+EKS7M+ZCGqwk845/tkmyvFGpfMwMd0m3NGdwB8/YYK80J38+ztSg8g7h283P3I4u7G7LsdwxGfsbHCR2XGb3st2/QchSHbqTveIZ4A1Oiu/gpMxPByV/59aoW2cHm6r8smFxjboej8cz11wDYXG20Csa/tIAAAAASUVORK5CYII=",
      nitroGrayIcon:
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAAB6ElEQVR4nO2WsYsTQRTG37eniFn/kMhVgiCC6L9gk0rEShFEBHNok11xZgicXIrrhKuEs8ofYGGl3YF3rals7axMRNzMJ082soQ53ZjNnZD5wS6PN/O+/ebtTDYikUgkst6gKaFer3cxSZKXGnvv7zvnPv4XBjudzka73X4E4DmAC5oj+ZXks9FotDscDqenZjDLsisAXgC4Hhon+Z7kE2vtwYka7Ha7aavVegpgC8D5P80l+Y3kzmQy2R4MBuOVG8yy7AaAHQCXF6kj+YHklrX23UoMatfSNDUiovtto/LgHyKyJyJ3AKRlTjv1SkTuAjhbmav7cXc8Hud1u5nU3Wtpmh4AeDxn7jvJq8aYB3p4KyVeczqmc2ZJrVUN1VLNxgyWB2EzMFRYaw8DWr/icqwI6G2qZmMGReTSLCD5aTqd3l6gPtGb1mhtSLMJg+f05r3fK4rimnNuXxbEObevtapR1fwbZ2rqvyH52lo7nMsnNWp/z+n3+59F5F6e529F5FZjBo0xN0N5ALW6ENDThc4vdqlXHISVE3oc/7qIRgxK4ITOU/4mnrxB7/1DETk6RqsaH5VzT/fvVp7nk9l3Wb+/xphWE7rLvuKVEw0uS+zgsqxVB5NV6DZpcJvkF700blA3EolE1pqfJ+bNaK9gV7oAAAAASUVORK5CYII=",
      mineBgd:
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAAA2ElEQVR4nM3ZsW0CQRRF0fuv6IAanFGBSW2ZBhy7EsuyqMSxG7AlUqiAzDXQAh/tLIFDMt6p4GlnJrlbzIqf9yfKNcUKesld1InmSJ/3bLY7oGuM+/14Ax6pfiBB1x9w4OXza/pyz5SvMeP+j+zz92I+1rBxk7HJtfOdC1WsvN+DuEUvJZyEk3ASTsJJOAkn4SSchJNwEk7CSTgJJ+EknISTcBJOwkk4CSfhJJyEk3ASTsI5smusOjmacKrm6AjWcxMmMAHvvdb0Q9TIvkb0zXY3Vf5J7G+IC3dRQRnp28FhAAAAAElFTkSuQmCC",
      mineWhiteIcon:
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAACs0lEQVR4nO2WTYiWVRTHf8dU8Cu/hhmtBL8/kgHJaIIWfSxauSsiwig3uhp0lyDuIoTARTViuBBEtIJAWlcQWdDCCOxLi9JpiGFmEgVHmZHuT+5wZ3p4Ed/XecdF8vzg4eU9z3PO/d977jn3Qk1NTU1NzQNNTMdJXQZsAHqAZ4GtamdELMgx1dGIGAF+Vc9GxDfAxYgYuq8C1aeA14E3gEXF9ldE/Ab8A9zIJmAekCexDlhb3MeAj4ETEfHFvQptJmyRetT/OK3uVNc1fDenPFMTVx9VX1E/VMeL/0cppcdmRFxK6WH16xL4jLqxMniHelg9p15Wh9WRvKrqD0XU1CSyqGLLXMzi2xaoHioBT6mzKvY16t8255q6qSHmu+XdpzMhcEJESmlLg31ZSqnf1uhp8F2hjqlXm40/uwWNE8UQETkdv5QBXgV2RcQK4GApjm7gEWCWOpgrOCL+APaqn6kngQ8i4k+gS30oIqYyMm1SSpfKKuQ9lQf7anJZUkpvNfNXV6m53Vj2Z18lxnDbAtXeO6TsVrbTeozl6oU7xHmv7RRHxPtqF3CgOibwTCmafmAAyKtxvbzLDbsDyK1kFfAksLQh9HFgH+2ibi2/L6vf3aUQRlNKV/KjXr/Ldz+qu0vMzpkQ+JP6XOX/jpwa9fdSic0YL9V+rDTsOSVOt/pt20ed+gnwEvA20Dd5nqpzgdVAZ0njSnXhRNCIm8CgmtM/FBH9ETFa/BYDb5Z4+Xze3u4Krq005HxSvKNuKwJbIh99eauo+9WByS2hPjEjlwV1M3AEeL5izj3uS+A8kG8uuemOV4pvCbAcyA3+BWBzZbzvgd6IaJrilskVq75Weti/To9z6h51/v2+Dz4OPA28CKwvLWT+ZNoj4lY5XfKqXgY+B84CP5d3NTU1NTU1Nf8TbgOzWhuUM2XFTgAAAABJRU5ErkJggg==",
      mineColorIcon:
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAADqUlEQVR4nO2WW2gcZRTHf//Zi42xF2tMrWlQu9k2bVoUDFFstVTBB9EXdZcEbLQvBrw/1AtKKcU+qKUg3qBVEBJDzCwoFvogVfEhVQQpiElNNhc1rVBqo4napk1258jMrksLJRtd+6DM72Fn5pvvnO9/9nznzAchISEhISH/a/SPrPrdpcSUJMJNyDaBmoBaoLro8xRwErNBUB+yQ9iZLA3tJy6uwFG3BdMWxINIC4Mxs6PAMGICs9MgA6owliISSCuL884CvRidNKQ+/XcFfvfOQuKLdyN1FBd7HzhAPvclq9pGS/O+3hsLrs0dOX9WcD/k1uFoAw53AFuRYpj1kj+zjVVbjlUuMNu1iMiCA0gbMe8jyD9Doi0bvBvsriEaex5pE1ADXFr0OQ1MAF+Rz71SCiLbtYLIgu1ID2M2TM42szr9U2UCR92XkPMs5vXQOfAAO3d6BXHudcR0CGn5nPZmvzFjLaxJD53jczdytmHeByTS981l7pQViNqDSy7/Yklc8MxU8FvWXItwtOS8sWn2YDYD8tM+J9HyAikUQ1R1/m4M7kfcVsRW4Co8bzvCL471YFdjcsCOIwYxjSF7kqj2M5J5j5y9QWP6e2LeMohE5vMHzSPFmR+QrsHsG8zeRboX6bbgnWfP0ZB6eU774Z56nOhBpNWYTWD0ItYFPsxOkkhdWVmKPdtTCEXX4zivFh3nyHtPlBXnk2w7ytnTG/Asi3QFjh4pBQg9lac4mX6dkcwyHL1wzqgFrWPE9QMcR94xZuxn5P2BlzMuqarG82rAWYGjer/xAJef59fPRmf/U5WneDjTRDI1wKh7P6ancdRywXnmN2n8ZuwTR6q+8DxvAOM1GtL7GOmsLfd1mc8eHMDjUZKpz4vPdwN3AncB9UjxOe3NZoHjGB9jHGTqlw9p7phl1F0P2ksidUtlAscyLobfq3Zh02+WIu5341Tlr4VILaZ6ZMsxLitaTWOBqHEsf4Lp2XFuaD9V7IGLMR5C2uW3bhKpGysTONS9kmi8L2jIftUZb5M3l1mOsC49U9b+r3WGM2tx7B7QY0h1wZbw8reSbD1cmUCfwe5GYvG3kDaXxszGMD4DvkW+cG8SURDsOVHEkqBqzdYg3Q40IhXWMzuMx+MkU1+Uj2y+7Njh0N7UCsGBYSPyG/LfxBdmto/JX7to7vCL6iKdB7PuWiK6uVgsDcUW4h8UCgVjzBa+LjYJ+hH4hFy+j9+njgQFEhISEhISEvKf4U8yp2JWyag2BQAAAABJRU5ErkJggg==",
      mineGrayIcon:
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAAEA0lEQVR4nO2WW2hcVRSG/3+fZDJ20mraGC9pUBvF2lp8UKLYqlTBBy8v1oAP3goSQY2SpLlM5sw8nMsQUwZEVHB8KKhQkxRFoQ+l6lNq8aUg0qoJ3toKYaxQxUySmTl7yQ5nJELNhKRFkPO9zMyetfb+1957/ecAERERERER/2u4mqSenp6NjY2NNwG4k+R9ALYDaAGQCOecBXAOwLcAJoMgODY/Pz+Vy+UKl1Tg8PBwR11d3VMAniG53oyJyBkA0wB+E5EiSRGRy0huBNBOcksYtwBgTETedV33s4sqsL+/f308Ht+vlHo+XOwDrfVhETnu+/731biurq5685nP5ysmLCyqVSm1Uyn1AIC9JOu11mMLCwv7RkdHz65ZYHd394ampqbDJHeJyMelUmlgZGRkyvzX29vbnEgkhsNjbgawLpxzzuwogC+DIBitFjEwMLA5Ho+nSXaJyHSlUtmdzWZ/WZPAdDo9opQa1FofdF33SQDajKdSqRssyzpG8prl8kXkD611h+d531XHMpnMfpL7RORDx3H2LJevagkk+XS4kFsVZygWi78DqKwgfwPJK5aOFYvFnIiUAJhjX5a6WgEAqs3QCuAb89227SeUUnsBXK21Tps1Se4AcG1Y9EzYwT8AeIXkJ5lM5v0gCN7wff/HeDx+FQBrRRtUKyCTyfxE8joR+QrAAQCPkbw3FD3kOM6ry+WnUqk2y7KOkrxZRMy9HANwq5lDRM45jnPlcvk1KwCQW6yEvI3ka+HEFRF5uZY4g+/7Z0ql0k4RmSK5ieQLSwo8eFFsJp1Oe0qpVPW3iJTNBSd5XEROAzhbKpV+LZfLf8ZiMeODCcuympVSm5VSbQDuAPAwyb93S2t9wHXd55be61UJtG17u+d5J23bflwp1U+y40JxIlIEYMzYECOZ+Je4kyLyuuu6+b6+vpZaT5eaTaKUGk+lUi96nncIwCHbth8h+SDJhwC0kYwtVkoaD1x3AUHlsGmOaK2PFgqFj/L5fNm27R1KqbcB3L3WJhkHYLzKm52dfbNacWdnZ6y9vf16y7JaLMtqExHjh42hWGPUM+b4RaQwNzd3OpfLmeczBgcHL29oaHjWzAdgynGc29ckcGhoaEssFps0hmy6TkTeCYJgfHp6+tTExITxspVA27a3kXyU5EskW82VqFQq92Sz2RNrEmhIJpNb6+vr3yK5uzomIsbjPheRr41wAOdD8zXUGXNWSm0SkVtI3g9gK8nF9UTkRBAE3b7vf1GzMqwcFRq0eWHYRXIlFvUPjDDzLjEzM/NePp83TXVp3geTyeQ2y7LuCpvlRgBNYYMsNgwA0xhGwHkAPwP4VGs9WSgUTpkGWc2aEREREREREfhP+AvTlb+SqTZnTwAAAABJRU5ErkJggg==",
      goldBgd:
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAAA10lEQVR4nM3ZoVUDURRF0fNuCUMNuFRALJSAAENtmCBoARsqGEcNSQk81htA4HA5u4K7/sw35xdAQ3FYbqneA7uuuuICqvsErHQdeTy/FXRt416WJ4qbhmsECj5o3nk4P1cfljvCvWXcn5GfvGY+q23c2DZV7zP/HF67XOpC/MdsmxNUC3JBLsgFuSAX5IJckAtyQS7IBbkgF+SCXJALckEuyAW5IBfkglyQC3JBLsgFufxkV6XqPs0JrnitmWC95VaZ2jp1HTM1fYK1aWT9RvTv0u9+hvgC/zVFk/2Io3IAAAAASUVORK5CYII=",
      goldWhiteIcon:
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAACmklEQVR4nO2Xv2sUURDH560JghIwEPVEOJMUgunURqtgLddYpBQsE6xDSrtYWPk/GDAQu1QGJAgpLBRBD+EKUYsENaiJxh/R+cjczYV12c3dxRdE2C8cb2fezPfNe7vzZk6kRIkSJUp0hKqOAwvAN+Crj6987lFKn/59t3mzy/gtGJ/EgqpeAZbIQFXXPYA1CuD+6zlTS8YbJUBgPkN+C6j1yHHR/dKYjxXgZoq0BlRV9WCPHIeAY+7fxmaUAFV1K3Vyp4C+vXLR8m+epPFGCRD43OLTC8DAX3INOI8abzc+HU8DuBpCqIQQVkVkZ9fAORFZFpE1EbHXdTbl9kREbDMVERkPITx2/ZbzXLfk2ssm8wI8AYxkdKOevStAPzDjn8E1H2dcv+J2oxn/EeONFeCX9pUBnLTXBDwDGsBxYAj4CMy5zZzLQz7fcPudz8NfMbEC/JEm29jYuOun9M4Xf+vyKrDtI65vuB3ml+LcuSc7IekmxkzAH/yx4d/gS9e/EJE3Porrl90u7RcXVrLSuwUSVX0A1F2u+oFMuzztctXlutsneznBngN0XQV4Diy6/DAVcN1kf150u0rGP2qA23lkwLAHfwOY8DUv+TjhepsfzvGNGqBl5K+CuZqvNQm8B+77ONkujXl+qvqziDPKPZiZn7VMVdWnlvE+WubO7uIT7x70dmvK7rSCxQJwM9OpmBxybA/43TgVs91q1mJgzLqSAptB4I4HZ+PgLl3NWC+1uNduxnafW79pZbYlydEiLveP3s380Q+q6hkvd/3dclj/uG/9YFFHDZzv0v/0vnbU//o/SV8ngyRJ7tkiIYRPInLZa3MIITQ/cuB1COFItma367zbHW77iYhVl9tJklidLlGiRIkS/zt+AzIgaZmhrRfaAAAAAElFTkSuQmCC",
      goldColorIcon:
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAAELElEQVR4nO1XTWhcVRg9571MJmZmnOmLYEVRKVaxQsFupKvYSUSiUrC2ETdSqiAEESEg2k1pN+rCjbi0CC5qG0Uk0mSRZDQlUHChVKEo6Y+t9ifSvElIm04yfe/IvZlMZiaJM0kGEZyzue9+P+eed9+7934XaKCBBhr4f4O1BIWZVDvBtwA+D0CFvAmm/YfCjPcDge0Feykcpv2oMt4lAPcu5emkoI+d9NRoLWM3VRU34u2h0AOHHaV2SfHCGz4IMrpavoA4y/zcw1DJcMRrczr8rzc8g8p4fSD3LSnWR1A4ys6pb6vlFjmGNu2Ew5fgsHfJqC+Z9rs3PIMAuopPQbAb4hnlpyawFtzOnkE0dh5uZBSu27+M9x/gVAsQ4BZnjvgZY9mrznOYW4s+7sYsn731l803PKW8GxVIIDQ/nAJ8hZkpn4dxB+vFzJRveSRZ3hpQ9RNLeJUMN9PFNcxjtmgfTu6A45qVeN0MDfLJkqSfACQAbEYYtLNz+kdrn8csE+E1AG9KjsmrwwzO504jr0F2TF1iNwI7/lByCxx3AMAvGPe3QeqzwaEOFAT2WbvxO+6AjTdc3QgMj+UzvPUQiGjLOTQ3XbDjDnv36xskCj/6DMK5F3FfPAnyXUhfsMP/zLS2b+zGb+Jct9/mLSLinkfLXVdrEVjLKo4sPrDTv6KMdwLkE5BuwImOIQ4jMAlplzJeHsAN2483nwUwDSAF8h7c7X0K+C8vELGmA6K2Gaw8IYRs4Wkc0iigi4WgXwH8UWiN5eKCH+MVeWtCLQLLccrvkfQ9oBQ7sq8jgN3ECQ0y7W8xrY0LsM/6oZSNP+X3/CsCeRghZ3Ov2KWQ8U7ymexlSGMA9hdC9pu+sRu/iTPxJm89Ams56vIgm7hrsixWg8mHEXV/g/Q+gLNwnBMI7qThNmUQhuZf2wbyPcwFj7Fr+vey3O/a7G9TybneGbwFadnb20HDcC8c5xCANkiTcJyDtjV9Yw/DvZXirECz7a/AuT6Bc7nHkQ8eWclFUzBIH4A8IuAKwHbbkkeMfbWCgvlgK+ZyD9RFoJpbdiLCLg3HTE23HGn/IISjJLeDjNhWOGrtlVx9cC2P4TO89RBI4nPA+QSItqkfrcv8gHA7+BDSsQUVOmb61l6JFkQNj+Fb4K2DQJkYs7ESB9AaS+jQ8s2dL0xnMZvrtYskN/+27a+E1ljC8pC0vHVaxaYQiC/Wgwqdc5z1/8QEcnwD5uSoinAAUbqxJNzIU8V6ULrJtL90/K2CWt5iYeM1cN1+ungN8dTT2Jo095Cq0FDiUUY27UBT9J2SYrWcd4Pl1nFKXvFOYst2t1eSb7YTZbzrIFdcQGafkxs5TdIrc4QaEXC8LgLNxSbMpCYpTpfe6gjctC8AXKaUWulWZ0WaOClWdqtj7be6BhpooIEG8N/G31Z+2K2uQoeIAAAAAElFTkSuQmCC",
      goldGrayIcon:
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAAEK0lEQVR4nO1WXWgcVRQ+5+7MZrPsUtN9sKAoFA1EoaAvpU+LPy9FEPyJ4ItIkTyIESEENpvM1NxJliwkL+rTogg+VEnEB0vtk+BCoUFQaQUR2tq02nbzYEtoHLo7u/fIF2ZlulnJpFlFcA8sZ8453/nmy82dew9R3/rWt779v43jgBzHyTPz20T0HBFJ2LeutX7Ydd1viehQmI+a0loPuK57hYjuj/SdEpH3PM+rxnm32gkwMzPzIjM7zAw/wMwpeCLKhJCHIvnoLxnWM9G+kMcBb09W0HXdZWYebcfGmCUiqnqed5JimuM4R4joJaXURDsnIita61d26rVi8B+NiHu+0Wic831/nXZh6+vr57LZ7KV0Ol1VSn3ZybtXgYlQ3JIx5vzq6ur1arXa3I3ASqXiE5E/PT19noiWwpXc4t3zHoQ2EZFWq/V5o9G4uVtxUUM/eMAHXurFChpjXmPmA8x8Y21tDSuxZcVi8UnLsvAl1ojoNjM/0a6JyA9ElCWiA81mM18qlb5HHv3Dw8M3ROQtEUHf3lfQGHPWGHN6fn7+ysrKSgu5QqFw0LKsr4jox1qt9piILIfCjoV+GXnUgQMeefSDB3zg7YlAy7IuWpb1C56LxeIDk5OT2WQyiY1+OwiCFzKZzD5mLhhjPtVafwyPGHnUgQMefW3ORCJxybbt6z0RSER2+6FUKl0bHBz8kJkfJ6L7bNs+k8lkfmJmiHzKdd0AHjHyqAMHPPraPMwc64KIK/CuG0JEboWPF0QEe/ByGP9MRL+GHnY5rF/o6NuVxRF4l3me96aIfIOV8TzvjXq9vnWIi8hprfVBeMTIow4c8Oj7VwTiu/F9/1Vocl33VLlcvioiZ5j5dRThESOPOnAh3vxTArdhFhcXa/V6HYPDs47jvGuMeZ+ZRxzHwf4bQYw86sABfy/i4gr8Q0S2/fXlcnlNRF5WSh1XSuVE5HdmLsIjRh514Dp7ce5347wngc1mc6TVaj3SreZ53kkRWSAiTUTXiCgfeo383w0UrVbr0Waz+WBPBCqljiiljk5NTWGm22Zaa6zaR8x8iJlteMTId2JHR0cT4AEfeHsl8BNm/oCIcmNjY+kuEAmCoCwiJ7YCkROIuwywNDQ0hDkyBz7w9kQgMDhYLcs6lsvlsvl8ftv9vbCwcMv3/QljzNNBELyDuBsR+sETHtSqVwMrBoFMex4UkYv1ev23jY2NO5VKJYjzkvHx8YFkMrkvnU4fbs+DIrKptf7r+tvLPIiDd+swBjnmwlQqVU2lUrhLv9upuVAoDNu2neucqEPenoxbnyml9jPzM6FIvGRCRG7iv+a6bo2Zu35As7OznEwmzzLz/mheRL4GbxyBO+6Dubm5L0TEExH4uojcgSeizRByNZKP/hphfTPaF/J44I0jsG9961vf+kb/bfsTu4dIhPtXB3oAAAAASUVORK5CYII=",
      pillar_bar:
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAABCAYAAACsXeyTAAAAEElEQVR4nGP8////f4ZBDAB45gP+CZUpMwAAAABJRU5ErkJggg==",
      pillar_top:
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAB4CAYAAACAcLCaAAABEElEQVR4nO2b0Q0EQQhC0aH/lr1cDe4HKq8CAoExd1nALCeqKiAM/yIhLjAhDCc4GBCGjriJI+7CqtKemYhwizt4Zk44mBCG8gJrwA4mhKF8xLDAHnawix38wsEHYeiZWR9x+Vjo4Yi/uAe1SwKfW9tLggECE8LQDjaxg19cM9otDj91B0ryIAz9kqyPuAbsYEIYykcMdYGl/ssC1B2MAcfCgzD0U9fEEZ9w8EEYThCYEIYTHHwQhvIR14CT/0EYykcMl6SHHezimTkxMwlh6B280OKEMHRJLjiYEIbewQsRJ4ShBa7/nyT8FvdwxF3s4P7vi8s7uD1i+Jrpl0Q74vA37gdaHBCGE2YmIAzVBf4AjmEyzvwc3hEAAAAASUVORK5CYII=",
    };
    self.SUPPLY_HUD_FONT = {
      0: {
        adv: 10500,
        path: [
          ["M", 5280, -1160],
          ["L", 5260, -1160],
          ["Q", 4000, -1160, 3260, -2640],
          ["Q", 2560, -4080, 2560, -6560],
          ["L", 2560, -6700],
          ["Q", 2560, -9200, 3300, -10680],
          ["Q", 4040, -12140, 5280, -12140],
          ["Q", 6640, -12140, 7340, -10660],
          ["Q", 7980, -9260, 7980, -6700],
          ["Q", 7980, -4080, 7300, -2660],
          ["Q", 6600, -1160, 5280, -1160],
          ["M", 5180, 240],
          ["L", 5200, 240],
          ["Q", 9800, 240, 9800, -6800],
          ["Q", 9800, -9960, 8660, -11740],
          ["Q", 7480, -13520, 5400, -13520],
          ["Q", 3300, -13520, 2060, -11740],
          ["Q", 740, -9880, 740, -6600],
          ["Q", 760, -3360, 2000, -1520],
          ["Q", 3180, 240, 5180, 240],
        ],
      },
      1: {
        adv: 10500,
        path: [
          ["M", 4840, -11620],
          ["L", 4840, 0],
          ["L", 6580, 0],
          ["L", 6580, -13300],
          ["L", 5040, -13300],
          ["L", 2140, -11740],
          ["L", 2480, -10380],
          ["L", 4800, -11620],
          ["L", 4840, -11620],
        ],
      },
      2: {
        adv: 10500,
        path: [
          ["M", 940, 0],
          ["L", 9440, 20],
          ["L", 9440, -1480],
          ["L", 3480, -1480],
          ["L", 3480, -1520],
          ["L", 4520, -2500],
          ["Q", 5700, -3660, 6420, -4420],
          ["Q", 7300, -5380, 7860, -6180],
          ["Q", 9100, -7960, 9100, -9660],
          ["Q", 9100, -11240, 8140, -12300],
          ["Q", 7040, -13520, 5040, -13520],
          ["Q", 3960, -13520, 2920, -13140],
          ["Q", 2000, -12780, 1280, -12180],
          ["L", 1860, -10900],
          ["Q", 2420, -11380, 3040, -11660],
          ["Q", 3860, -12040, 4680, -12040],
          ["Q", 6080, -12040, 6720, -11240],
          ["Q", 7300, -10580, 7300, -9440],
          ["Q", 7300, -8020, 6100, -6420],
          ["Q", 5520, -5640, 4560, -4640],
          ["Q", 3760, -3820, 2340, -2460],
          ["L", 940, -1100],
          ["L", 940, 0],
        ],
      },
      3: {
        adv: 10500,
        path: [
          ["M", 1360, -2040],
          ["L", 880, -660],
          ["Q", 1420, -300, 2280, -60],
          ["Q", 3280, 240, 4340, 240],
          ["Q", 6600, 240, 7960, -980],
          ["Q", 9180, -2080, 9180, -3760],
          ["Q", 9180, -5120, 8320, -6060],
          ["Q", 7500, -6920, 6220, -7160],
          ["L", 6220, -7200],
          ["Q", 8680, -8100, 8680, -10360],
          ["Q", 8680, -11680, 7760, -12540],
          ["Q", 6700, -13520, 4860, -13520],
          ["Q", 3860, -13520, 2840, -13200],
          ["Q", 1960, -12940, 1360, -12500],
          ["L", 1860, -11200],
          ["Q", 2340, -11540, 3000, -11780],
          ["Q", 3780, -12080, 4520, -12080],
          ["Q", 5680, -12080, 6300, -11480],
          ["Q", 6860, -10920, 6860, -10060],
          ["Q", 6860, -8980, 5860, -8340],
          ["Q", 5000, -7820, 3880, -7820],
          ["L", 2880, -7820],
          ["L", 2880, -6460],
          ["L", 3880, -6460],
          ["Q", 5320, -6460, 6260, -5820],
          ["Q", 7300, -5120, 7320, -3800],
          ["L", 7320, -3740],
          ["Q", 7320, -2780, 6680, -2080],
          ["Q", 5880, -1220, 4320, -1220],
          ["Q", 3480, -1220, 2540, -1500],
          ["Q", 1880, -1740, 1360, -2040],
        ],
      },
      4: {
        adv: 10500,
        path: [
          ["M", 6500, -5020],
          ["L", 2140, -5020],
          ["L", 2140, -5060],
          ["L", 5400, -9600],
          ["Q", 5960, -10560, 6500, -11560],
          ["L", 6560, -11560],
          ["Q", 6500, -10520, 6500, -9480],
          ["L", 6500, -5020],
          ["M", 6500, 0],
          ["L", 8200, 20],
          ["L", 8200, -3640],
          ["L", 10060, -3640],
          ["L", 10060, -5020],
          ["L", 8200, -5020],
          ["L", 8200, -13300],
          ["L", 6260, -13300],
          ["L", 320, -4800],
          ["L", 320, -3640],
          ["L", 6500, -3640],
          ["L", 6500, 0],
        ],
      },
      5: {
        adv: 10500,
        path: [
          ["M", 8880, -11780],
          ["L", 8880, -13300],
          ["L", 2520, -13300],
          ["L", 1660, -6920],
          ["Q", 2520, -7060, 3520, -7060],
          ["Q", 7320, -7060, 7320, -4120],
          ["L", 7320, -4060],
          ["Q", 7320, -2800, 6400, -2000],
          ["Q", 5500, -1220, 4180, -1220],
          ["Q", 3340, -1220, 2500, -1460],
          ["Q", 1820, -1660, 1320, -1960],
          ["L", 880, -580],
          ["Q", 1420, -240, 2220, -40],
          ["Q", 3160, 240, 4180, 240],
          ["Q", 6340, 240, 7760, -1060],
          ["Q", 9140, -2340, 9140, -4320],
          ["Q", 9140, -5500, 8600, -6440],
          ["Q", 8080, -7260, 7240, -7740],
          ["Q", 6060, -8440, 4380, -8440],
          ["Q", 4040, -8440, 3760, -8420],
          ["L", 3280, -8360],
          ["L", 3800, -11780],
          ["L", 8880, -11780],
        ],
      },
      6: {
        adv: 10500,
        path: [
          ["M", 5440, -1160],
          ["L", 5420, -1160],
          ["Q", 4040, -1160, 3240, -2240],
          ["Q", 2500, -3280, 2480, -4980],
          ["Q", 2480, -5400, 2680, -5760],
          ["Q", 3500, -7400, 5280, -7400],
          ["Q", 8020, -7400, 8020, -4360],
          ["Q", 8020, -2920, 7300, -2040],
          ["Q", 6580, -1160, 5440, -1160],
          ["M", 8520, -12040],
          ["L", 8520, -13500],
          ["Q", 7900, -13500, 7220, -13400],
          ["Q", 4640, -13100, 2960, -11440],
          ["Q", 1920, -10420, 1340, -8940],
          ["Q", 700, -7340, 700, -5440],
          ["Q", 700, -2860, 2000, -1300],
          ["Q", 3260, 240, 5400, 240],
          ["Q", 7400, 240, 8680, -1200],
          ["Q", 9840, -2560, 9840, -4480],
          ["Q", 9840, -6400, 8720, -7580],
          ["Q", 7600, -8780, 5820, -8780],
          ["Q", 4780, -8780, 3900, -8320],
          ["Q", 3100, -7920, 2580, -7220],
          ["L", 2540, -7220],
          ["Q", 2800, -9000, 3920, -10240],
          ["Q", 5140, -11600, 7180, -11940],
          ["Q", 7880, -12040, 8360, -12040],
          ["L", 8520, -12040],
        ],
      },
      7: {
        adv: 10500,
        path: [
          ["M", 9580, -13300],
          ["L", 1180, -13300],
          ["L", 1180, -11800],
          ["L", 7680, -11800],
          ["L", 7680, -11760],
          ["L", 1940, 0],
          ["L", 3780, 0],
          ["L", 9580, -12120],
          ["L", 9580, -13300],
        ],
      },
      8: {
        adv: 10500,
        path: [
          ["M", 5300, -12240],
          ["L", 5320, -12240],
          ["Q", 6420, -12240, 7040, -11560],
          ["Q", 7540, -10960, 7540, -10060],
          ["Q", 7540, -8340, 5520, -7660],
          ["Q", 2980, -8320, 2980, -10080],
          ["L", 2980, -10100],
          ["Q", 2980, -11000, 3580, -11600],
          ["Q", 4200, -12240, 5300, -12240],
          ["M", 5280, -1080],
          ["L", 5260, -1080],
          ["Q", 4020, -1080, 3280, -1840],
          ["Q", 2600, -2520, 2600, -3480],
          ["L", 2600, -3600],
          ["Q", 2600, -5660, 5000, -6360],
          ["Q", 6440, -5960, 7160, -5320],
          ["Q", 7940, -4600, 7940, -3460],
          ["Q", 7940, -1080, 5280, -1080],
          ["M", 5400, -13520],
          ["L", 5380, -13520],
          ["Q", 3600, -13520, 2440, -12540],
          ["Q", 1260, -11540, 1260, -9980],
          ["Q", 1260, -7980, 3380, -7000],
          ["L", 3380, -6940],
          ["Q", 760, -5800, 760, -3340],
          ["Q", 760, -1840, 1960, -820],
          ["Q", 3200, 240, 5240, 240],
          ["Q", 7180, 240, 8440, -820],
          ["Q", 9760, -1920, 9760, -3640],
          ["Q", 9760, -6020, 7080, -7080],
          ["L", 7080, -7140],
          ["Q", 8220, -7660, 8800, -8540],
          ["Q", 9280, -9320, 9280, -10240],
          ["Q", 9280, -11600, 8300, -12520],
          ["Q", 7240, -13520, 5400, -13520],
        ],
      },
      9: {
        adv: 10500,
        path: [
          ["M", 5240, -12160],
          ["L", 5260, -12160],
          ["Q", 6600, -12160, 7320, -11000],
          ["Q", 7980, -9980, 7980, -8240],
          ["L", 7980, -8140],
          ["Q", 7980, -7780, 7840, -7480],
          ["Q", 6980, -6100, 5200, -6100],
          ["Q", 2640, -6100, 2640, -8920],
          ["Q", 2640, -10340, 3360, -11260],
          ["Q", 4100, -12160, 5240, -12160],
          ["M", 1980, -1260],
          ["L", 1980, 220],
          ["L", 2160, 220],
          ["Q", 2720, 220, 3640, 120],
          ["Q", 5940, -160, 7500, -1660],
          ["Q", 8560, -2700, 9140, -4160],
          ["Q", 9800, -5820, 9800, -7920],
          ["Q", 9800, -10500, 8620, -12020],
          ["Q", 7420, -13520, 5400, -13520],
          ["Q", 3400, -13520, 2100, -12120],
          ["Q", 820, -10740, 820, -8820],
          ["Q", 820, -7060, 1900, -5900],
          ["Q", 3000, -4720, 4760, -4720],
          ["Q", 6740, -4720, 7920, -6160],
          ["L", 7980, -6160],
          ["Q", 7620, -3920, 6220, -2620],
          ["Q", 5060, -1520, 3420, -1340],
          ["Q", 2800, -1260, 2280, -1260],
          ["L", 1980, -1260],
        ],
      },
      _meta: {
        em: 20480,
        ascent: 18200,
        descent: 5120,
        leading: 3400,
      },
    };

    /*
     * supply-logic.js  --  DOM-free port of the battle HUD "supply" (inventory) slot logic.
     *
     * Every class below is a hand translation of ActionScript 3 bytecode that was READ
     * (never executed) from battles.swf. The original (obfuscated) class each one comes
     * from is noted so the port can be audited against the SWF:
     *
     *   Blinker              8jzwdp.1lpgiu      icon alpha blinking
     *   BlinkConfig          4gtwf6.4e61od      blink periods / intervals
     *   SplashColors         5nfq0s.3o5ts8      flash colours per slot
     *   Splash               5nfq0s.2bl3um      100ms flash-in / 250ms fade-out
     *   SectorView           4aoblm.9a9uj0 +    pie-wipe masked bitmap
     *                        0wmc7r.7y26r4
     *   EffectController     4aoblm.1xnexd      "supply is active" countdown
     *   CooldownController   4aoblm.7dvkv1      "supply is recharging" countdown
     *   CooldownItem         4aoblm.2shp5q      effect + cooldown pair
     *   LightPillar          8ujmkc.0fjr65      500ms "ready" beam
     *   SupplySlot           3lbfx9.1tywfv      one HUD slot
     *   SupplyPanel          3lbfx9.1cm5rf      the 5(+1) slot panel, keys, locks
     *
     * Time is always passed in / injected (ms, like flash.utils.getTimer()), so the
     * whole thing is deterministic and testable with a virtual clock.
     */
    (function (root, factory) {
      if (typeof module === "object" && module.exports)
        module.exports = factory();
      else root.SupplyLogic = factory();
    })(typeof self !== "undefined" ? self : this, function () {
      "use strict";

      // ---------------------------------------------------------------- constants
      /** 1-based slot numbers as used by the SWF (also the key shown in the slot). */
      var SLOT = {
        FIRST_AID: 1,
        ARMOR: 2,
        DAMAGE: 3,
        NITRO: 4,
        MINE: 5,
        GOLD: 6,
        DRONE: 7,
      };
      var SLOT_NAMES = {
        1: "firstAid",
        2: "armor",
        3: "damage",
        4: "nitro",
        5: "mine",
        6: "gold",
      };
      var ICON_STATE = {
        NEUTRAL: 0,
        EFFECT: 1,
        COOLDOWN: 2,
      };
      /** setLockMask bits. 1/4/8 are directly evidenced by callers; 2 is what the server lock uses. */
      var LOCK = {
        DEAD: 1,
        SERVER: 2,
        UI: 4,
        CUP: 8,
      };
      var QUANTITY_COLOR_WHITE = 0xffffff;
      var QUANTITY_COLOR_GRAY = 0xb2b2b2; // 11711154
      var SLOT_SIZE = 40;
      var SLOT_PITCH = 50; // 10px gap + 40px icon
      var FILL_EFFECT_TIME = 100; // 0.1 s * 1000
      var TOLERANCE_EFFECT_TIME = 300;

      function clamp01(v) {
        return Math.max(0, Math.min(1, v));
      }

      function toInt(v) {
        v = +v;
        return v < 0 ? Math.ceil(v) : Math.floor(v);
      } // AS3 int()

      // ------------------------------------------------------------------ tweens
      /** Minimal stand-in for utils.tweener.TweenLite (only .to + killTweensOf are used). */
      function TweenManager() {
        this.list = [];
      }
      TweenManager.prototype.to = function (clock, target, seconds, vars) {
        var t = {
          target: target,
          start: clock(),
          dur: seconds * 1000,
          vars: vars,
          from: {},
          done: false,
        };
        for (var k in vars)
          if (k !== "onComplete" && k !== "ease") t.from[k] = target[k];
        this.list.push(t);
        return t;
      };
      TweenManager.prototype.killTweensOf = function (target) {
        this.list = this.list.filter(function (t) {
          return t.target !== target;
        });
      };
      TweenManager.prototype.update = function (now) {
        var pending = this.list.slice();
        for (var i = 0; i < pending.length; i++) {
          var t = pending[i];
          if (this.list.indexOf(t) < 0) continue; // killed meanwhile
          var p =
            t.dur <= 0 ? 1 : Math.min(1, Math.max(0, (now - t.start) / t.dur));
          var e = 1 - (1 - p) * (1 - p); // TweenLite default ease: Quad.easeOut
          for (var k in t.from)
            t.target[k] = t.from[k] + (t.vars[k] - t.from[k]) * e;
          if (p >= 1) {
            this.list.splice(this.list.indexOf(t), 1);
            if (t.vars.onComplete) t.vars.onComplete();
          }
        }
      };

      // ----------------------------------------------------------------- blinker
      var BlinkConfig = {
        EFFECT_WARNING_PERIOD: 3000,
        FIRST_AID_WARNING_PERIOD: 100000,
        INITIAL_BLINK_INTERVAL: 300,
        MIN_INTERVAL: 20,
        BLINK_INTERVAL_DECREMENT: 30,
        MIN_VALUE: 0.2,
        MAX_VALUE: 1,
        SPEED_COEFF: 10,
        getBlinkingPeriod: function (slot) {
          return slot === 1 ? 100000 : 3000;
        },
        createBlinker: function (slot) {
          return new Blinker(300, 20, slot === 1 ? 0 : 30, 0.2, 1.0, 10.0);
        },
      };

      function Blinker(
        initialInterval,
        minInterval,
        intervalDecrement,
        minValue,
        maxValue,
        speedCoeff,
      ) {
        this.initialInterval = initialInterval;
        this.minInterval = minInterval;
        this.intervalDecrement = intervalDecrement;
        this.minValue = minValue;
        this.maxValue = maxValue;
        this.speedCoeff = speedCoeff;
        this.valueDelta = maxValue - minValue;
        this.value = 0;
        this.speed = 0;
        this.switchTime = 0;
        this.currInterval = 0;
      }
      Blinker.prototype.getSpeed = function (dir) {
        return (dir * this.speedCoeff * this.valueDelta) / this.currInterval;
      };
      Blinker.prototype.init = function (now) {
        this.value = this.maxValue;
        this.currInterval = this.initialInterval;
        this.speed = this.getSpeed(-1);
        this.switchTime = now + this.currInterval;
      };
      Blinker.prototype.setMaxValue = function (v) {
        if (!(v > this.minValue)) return;
        this.maxValue = v;
        this.valueDelta = this.maxValue - this.minValue;
      };
      Blinker.prototype.setMinValue = function (v) {
        if (!(v < this.maxValue)) return;
        this.minValue = v;
        this.valueDelta = this.maxValue - this.minValue;
      };
      Blinker.prototype.updateValue = function (now, dt) {
        this.value = this.value + this.speed * dt;
        if (this.value > this.maxValue) this.value = this.maxValue;
        if (this.value < this.minValue) this.value = this.minValue;
        if (now >= this.switchTime) {
          if (this.currInterval > this.minInterval) {
            this.currInterval -= this.intervalDecrement;
            if (this.currInterval < this.minInterval)
              this.currInterval = this.minInterval;
          }
          this.switchTime = now + this.currInterval;
          this.speed = this.speed < 0 ? this.getSpeed(1) : this.getSpeed(-1);
        }
        return this.value;
      };

      // ------------------------------------------------------------------ splash
      /** Colours are 0xRRGGBBAA, exactly as stored in the SWF. */
      var SplashColors = {
        COOLDOWN: 126, // 0x0000007E -> translucent black
        getColor: function (slot) {
          switch (slot | 0) {
            case 0:
              return 495830; // overdrive/ultimate
            case 1:
              return 3135184319; // BADF1DBF
            case 2:
              return 3819201471; // E3A463BF
            case 3:
              return 4285085631; // FF6937BF
            case 4:
              return 4293333951; // FFE713BF
            case 5:
              return 14527935; // 00DDADBF
            case 6:
              return 4289270719; // FFA913BF
            default:
              return 4294967295;
          }
        },
      };
      /** Ready-indicator (light pillar) colours, 0xRRGGBB. */
      function getIndicatorColor(slot) {
        switch (slot | 0) {
          case 1:
            return 10210624;
          case 2:
            return 15248503;
          case 3:
            return 15623237;
          case 4:
            return 15648305;
          case 5:
            return 3193743;
          case 6:
            return 16760654;
          case 7:
            return 16777215;
          default:
            return 0;
        }
      }

      var SPLASH_SHOW_DURATION = 100,
        SPLASH_FADE_DURATION = 250;
      var SPLASH_SHOW = 1,
        SPLASH_FADE = 2,
        SPLASH_IDLE = 3;

      /** Applies a ColorTransform to `target.ct` ({rm,gm,bm,am,ro,go,bo,ao}). */
      function Splash(target, clock) {
        this.target = target;
        this.clock = clock;
        this.state = SPLASH_IDLE;
        this.color = 0;
        this.startTime = 0;
        this.cb = null;
      }
      Splash.prototype.interpolate = function (color, t) {
        var c = color >>> 0,
          m = 1 - t;
        this.target.ct = {
          rm: m,
          gm: m,
          bm: m,
          am: m,
          ro: ((c >>> 24) & 255) * t,
          go: ((c >>> 16) & 255) * t,
          bo: ((c >>> 8) & 255) * t,
          ao: (c & 255) * t,
        };
      };
      Splash.prototype.startFlash = function (color, cb) {
        this.stopFlash();
        this.cb = cb || null;
        this.color = color >>> 0;
        this.startTime = this.clock();
        this.state = SPLASH_SHOW;
      };
      Splash.prototype.stopFlash = function () {
        if (this.state !== SPLASH_IDLE) {
          this.state = SPLASH_IDLE;
          this.interpolate(this.color, 0);
        }
      };
      Splash.prototype.update = function (now) {
        if (this.state === SPLASH_IDLE) return;
        if (this.state === SPLASH_SHOW) {
          if (now < this.startTime + SPLASH_SHOW_DURATION) {
            this.interpolate(
              this.color,
              (now - this.startTime) / SPLASH_SHOW_DURATION,
            );
          } else {
            this.interpolate(this.color, 1);
            this.startTime =
              this.startTime + SPLASH_SHOW_DURATION + SPLASH_FADE_DURATION;
            this.state = SPLASH_FADE;
            if (this.cb !== null) {
              var f = this.cb;
              f();
              this.cb = null;
            }
          }
        } else if (this.state === SPLASH_FADE) {
          if (now < this.startTime)
            this.interpolate(
              this.color,
              (this.startTime - now) / SPLASH_FADE_DURATION,
            );
          else this.stopFlash();
        }
      };

      // ------------------------------------------------------------- sector view
      /**
       * A bitmap (the layer `image`) masked by a pie slice. Records the slice the
       * original Pen.drawArc call would draw: centre (size/2,size/2), radius
       * ceil(size*sqrt(2)/2), sweeping CLOCKWISE (screen space) from startDeg.
       */
      function SectorView(size, image) {
        this.size = size;
        this.image = image;
        this.visible = true;
        this.radius = Math.ceil(Math.sqrt(size * size + size * size) / 2);
        this.startProgress = NaN;
        this.endProgress = NaN;
        this.startDeg = 0;
        this.sweepDeg = 0; // sweep 0 => mask empty => nothing visible
        this.version = 0;
      }
      SectorView.prototype.setProgress = function (a, b) {
        if (this.startProgress === a && this.endProgress === b) return;
        this.startProgress = a;
        this.endProgress = b;
        var s = 360 * clamp01(a),
          e = 360 * clamp01(b);
        this.sweepDeg = e - s;
        this.startDeg = s - 90;
        this.version++;
      };

      // --------------------------------------------------------- progress record
      function ProgressData() {
        this.reset();
      }
      ProgressData.prototype.reset = function () {
        this.progress = 1;
        this.fillProgress = 1;
        this.addingProgress = 1;
      };

      // -------------------------------------------------------- effect controller
      function EffectController(
        slot,
        icon,
        view,
        splash,
        finishCb,
        finishFillCb,
        blinkEnabled,
        env,
      ) {
        this.slot = slot;
        this.icon = icon;
        this.view = view;
        this.splash = splash;
        this.finishCallback = finishCb;
        this.finishFillEffectCallBack = finishFillCb;
        this.blinkEnabled = blinkEnabled === undefined ? true : blinkEnabled;
        this.clock = env.clock;
        this.tweens = env.tweens;
        this.data = new ProgressData();
        this.startTime = 0;
        this.durationTime = 0;
        this.isFillEffect = false;
        this.isActive = false;
        this.isAddingEffect = false;
        this.blinkingPeriod = BlinkConfig.getBlinkingPeriod(slot);
        this.blinker = BlinkConfig.createBlinker(slot);
        this.lastUpdateTime = -1;
        this.blinkerInited = false;
        this.effectStopped = false;
      }
      var EP = EffectController.prototype;
      EP.update = function (time) {
        var dt = 0;
        if (!this.isActive) return;
        if (this.lastUpdateTime < 0) {
          this.lastUpdateTime = time;
        } else {
          dt = toInt(time - this.lastUpdateTime);
          this.lastUpdateTime = time;
          if (
            this.blinkEnabled &&
            time - this.startTime > this.durationTime - this.blinkingPeriod
          ) {
            if (!this.effectStopped) this.blinkIcon(time, dt);
          } else if (this.blinkerInited) {
            this.resetVarsForBlink();
          }
        }
        if (this.isFillEffect || this.isAddingEffect) return;
        this.data.progress = this.calculateProgress(
          time,
          this.startTime,
          this.durationTime,
        );
        if (this.data.progress > 1) {
          this.data.progress = 1;
          this.finish();
        }
        this.view.setProgress(this.data.progress, 1);
      };
      EP.calculateProgress = function (time, start, dur) {
        return toInt(time - start) / dur;
      };
      EP.getProgress = function () {
        if (this.isAddingEffect) return this.data.addingProgress;
        if (this.isFillEffect) return this.data.fillProgress;
        return this.data.progress;
      };
      EP.getRemainingTime = function () {
        if (this.durationTime === 0 || this.startTime === -1) return 0;
        return this.durationTime - (this.clock() - this.startTime);
      };
      EP.destroy = function () {
        if (!this.isActive) return;
        this.isActive = false;
        this.isFillEffect = false;
        this.isAddingEffect = false;
        this.startTime = -1;
        this.durationTime = 0;
        this.data.reset();
        this.view.setProgress(0, 0);
        this.tweens.killTweensOf(this.data);
        this.resetVarsForBlink();
      };
      EP.changeTime = function (time, dur, flag) {
        var remaining = 0,
          diff = 0;
        this.effectStopped = false;
        if (!this.isActive) {
          this.activate(time, dur, flag);
          return;
        }
        if (dur <= 0) {
          this.finish();
          return;
        }
        remaining = toInt(this.getRemainingTime());
        diff = toInt(dur - remaining);
        if (diff > TOLERANCE_EFFECT_TIME) {
          this.activateAddingEffect(time, dur, flag);
          return;
        }
        if (diff < -TOLERANCE_EFFECT_TIME) this.retime(time, dur);
      };
      EP.retime = function (time, dur) {
        this.tweens.killTweensOf(this.data);
        this.isFillEffect = false;
        this.isAddingEffect = false;
        this.data.fillProgress = 0;
        this.data.addingProgress = 0;
        this.startTime = time;
        this.durationTime = dur;
        this.resetVarsForBlink();
        this.data.progress = 0;
        this.view.setProgress(0, 1);
      };
      EP.stopEffect = function () {
        this.resetVarsForBlink();
        this.effectStopped = true;
        this.startTime = -1;
        this.durationTime = 0;
      };
      EP.blinkIcon = function (time, dt) {
        if (this.blinkerInited) {
          var v = this.blinker.updateValue(time, dt);
          if (v !== this.icon.alpha) this.icon.alpha = v;
        } else {
          this.blinker.setMinValue(0.2);
          this.blinker.init(time);
          this.blinkerInited = true;
        }
      };
      EP.finish = function () {
        this.tweens.killTweensOf(this.data);
        this.isFillEffect = false;
        this.isAddingEffect = false;
        this.isActive = false;
        this.startTime = -1;
        this.durationTime = 0;
        this.resetVarsForBlink();
        this.data.reset();
        this.view.setProgress(this.data.progress, 1);
        this.finishCallback();
      };
      EP.activate = function (time, dur, flag) {
        if (this.isActive || dur <= 0) return;
        this.isActive = true;
        this.activateFillEffect(time, dur, flag);
      };
      EP.activateInfinite = function () {
        this.isActive = true;
        this.data.progress = 0;
        this.data.fillProgress = 1;
        this.durationTime = 2147483647;
      };
      EP.activateFillEffect = function (time, dur, flag) {
        if (this.isFillEffect) {
          this.changeTime(time, dur, flag);
          return;
        }
        var d = toInt(dur - FILL_EFFECT_TIME);
        if (d > TOLERANCE_EFFECT_TIME) {
          var self = this;
          this.isFillEffect = true;
          this.splash.startFlash(SplashColors.getColor(this.slot));
          this.startTime = time + FILL_EFFECT_TIME;
          this.durationTime = d;
          this.data.progress = 0;
          this.data.fillProgress = 1;
          this.tweens.to(this.clock, this.data, 0.1, {
            fillProgress: 0,
            onComplete: function () {
              self.onCompleteFill();
            },
          });
        }
      };
      EP.onCompleteFill = function () {
        this.data.fillProgress = 0;
        this.view.setProgress(0, 1);
        this.finishFillEffectCallBack();
        this.isFillEffect = false;
      };
      EP.activateAddingEffect = function (time, dur, flag) {
        if (this.isAddingEffect) {
          this.changeTime(time, dur, flag);
          return;
        }
        var d = toInt(dur - FILL_EFFECT_TIME);
        if (d > 0) {
          var self = this;
          this.isAddingEffect = true;
          this.splash.startFlash(SplashColors.getColor(this.slot));
          this.startTime = time + FILL_EFFECT_TIME;
          this.durationTime = d;
          this.data.addingProgress = this.data.progress;
          this.tweens.to(this.clock, this.data, 0.1, {
            addingProgress: 0,
            onComplete: function () {
              self.onCompleteAdding();
            },
          });
        }
      };
      EP.onCompleteAdding = function () {
        this.data.addingProgress = 0;
        this.view.setProgress(0, 1);
        this.isAddingEffect = false;
      };
      EP.resetVarsForBlink = function () {
        this.icon.alpha = 1;
        this.blinkerInited = false;
      };

      // ------------------------------------------------------ cooldown controller
      function CooldownController(
        slot,
        view,
        splash,
        finishCb,
        finishFillCb,
        env,
      ) {
        this.slot = slot;
        this.view = view;
        this.splash = splash;
        this.finishCallback = finishCb;
        this.finishFillCallback = finishFillCb;
        this.clock = env.clock;
        this.tweens = env.tweens;
        this.data = new ProgressData();
        this.startTime = 0;
        this.durationTime = 0;
        this.isActive = false;
        this.isFillEffect = false;
        this.isAddingEffect = false;
        this.effectProgress = 1;
        this.fixProgressTime = 0;
        this.fixProgress = 0;
        this.lastProgress = 0;
        this.lastTime = 0;
      }
      var CP = CooldownController.prototype;
      CP.update = function (time, effProgress) {
        this.effectProgress = effProgress;
        if (!this.isActive) return;
        if (this.isFillEffect) return;
        if (this.isAddingEffect) return;
        if (time >= this.startTime) {
          this.data.progress = this.calculateProgress(time);
          this.lastTime = time;
          this.lastProgress = this.data.progress;
          if (this.data.progress > 1) {
            this.data.progress = 1;
            this.finish();
          }
        }
        this.setProgress(this.data.progress, effProgress);
      };
      CP.setProgress = function (a, b) {
        if (a < b) this.view.setProgress(a, b);
        else this.view.setProgress(0, 0);
      };
      CP.finish = function () {
        this.isActive = false;
        this.startTime = -1;
        this.view.setProgress(0, 0);
        this.finishCallback();
        this.fixProgressTime = 0;
        this.fixProgress = 0;
      };
      CP.calculateProgress = function (time) {
        if (this.fixProgressTime !== 0)
          return (
            ((time - this.fixProgressTime) * (1 - this.fixProgress)) /
              this.durationTime +
            this.fixProgress
          );
        return (time - this.startTime) / this.durationTime;
      };
      CP.isCooldownStarted = function () {
        return this.data.progress !== 0;
      };
      CP.destroy = function () {
        if (!this.isActive) return;
        this.isActive = false;
        this.isFillEffect = false;
        this.isAddingEffect = false;
        this.startTime = -1;
        this.view.setProgress(0, 0);
        this.fixProgressTime = 0;
        this.fixProgress = 0;
        this.tweens.killTweensOf(this.data);
      };
      CP.activate = function (start, dur, effActive, flag2) {
        if (!(dur > 0)) return;
        this.isActive = true;
        this.startTime = start;
        this.durationTime = dur;
        this.fixProgressTime = 0;
        this.fixProgress = 0;
        this.activateFillMode(effActive, flag2);
      };
      CP.needActivateWithSplash = function () {
        return this.slot === 5 || this.slot === 6 || this.slot === 0;
      };
      CP.activateFillMode = function (effActive, flag2) {
        if (this.isFillEffect) return;
        var self = this;
        this.isFillEffect = true;
        if (!effActive) {
          if (this.needActivateWithSplash() && !flag2)
            this.splash.startFlash(SplashColors.getColor(this.slot));
          else this.splash.startFlash(SplashColors.COOLDOWN);
        }
        this.data.progress = 0;
        this.data.fillProgress = 1;
        this.view.setProgress(this.data.fillProgress, 1);
        this.tweens.to(this.clock, this.data, 0.1, {
          fillProgress: 0,
          onComplete: function () {
            self.onCompleteFill();
          },
        });
      };
      CP.onCompleteFill = function () {
        this.data.fillProgress = 0;
        this.setProgress(0, this.effectProgress);
        this.finishFillCallback();
        this.isFillEffect = false;
      };
      CP.changeTime = function (time, dur, effActive, flag4) {
        var remaining = 0,
          diff = 0,
          extra = 0;
        if (this.isActive) {
          remaining = toInt(this.getRemainingTime());
          diff = toInt(dur - remaining);
          if (diff > TOLERANCE_EFFECT_TIME) {
            if (this.isFillEffect || this.isAddingEffect) {
              if (this.isFillEffect)
                extra = toInt(dur - (remaining + FILL_EFFECT_TIME));
              if (this.isAddingEffect)
                extra = toInt(dur - (remaining + FILL_EFFECT_TIME));
              if (extra > TOLERANCE_EFFECT_TIME)
                this.durationTime = this.durationTime + extra;
            } else {
              this.activateAddingEffect(time, dur, effActive, flag4);
            }
          }
        } else {
          this.activate(
            toInt(time + FILL_EFFECT_TIME),
            toInt(dur - FILL_EFFECT_TIME),
            effActive,
            flag4,
          );
        }
      };
      CP.activateInfinite = function () {
        this.isActive = true;
        this.startTime = 0;
        this.durationTime = 2147483647;
        this.activateFillMode(true, false);
      };
      CP.setCooldownDuration = function (ms) {
        this.fixProgressTime = this.lastTime;
        this.fixProgress = this.lastProgress;
        this.durationTime = ms;
        this.startTime = this.lastTime;
      };
      CP.startCooldownNow = function (time) {
        this.destroy();
        this.activate(time, this.durationTime, false, false);
      };
      CP.activateAddingEffect = function (time, dur, effActive, flag4) {
        if (this.isAddingEffect) {
          this.changeTime(time, dur, effActive, flag4);
          return;
        }
        var d = toInt(dur - FILL_EFFECT_TIME);
        if (d > TOLERANCE_EFFECT_TIME) {
          var self = this;
          this.isAddingEffect = true;
          if (!effActive) this.splash.startFlash(SplashColors.COOLDOWN);
          this.startTime = time + FILL_EFFECT_TIME;
          this.durationTime = d;
          this.data.addingProgress = this.data.progress;
          this.tweens.to(this.clock, this.data, 0.1, {
            addingProgress: 0,
            onComplete: function () {
              self.onCompleteAdding();
            },
          });
        }
      };
      CP.onCompleteAdding = function () {
        this.data.addingProgress = 0;
        this.setProgress(0, this.effectProgress);
        this.isAddingEffect = false;
      };
      CP.getRemainingTime = function () {
        if (this.durationTime === 0 || this.startTime === -1) return 0;
        return this.durationTime - (this.clock() - this.startTime);
      };
      CP.setVisible = function (v) {
        this.view.visible = v;
      };

      // ---------------------------------------------------------- cooldown item
      function CooldownItem(
        slot,
        effectView,
        cooldownView,
        splash,
        icon,
        finishCb,
        startCooldownCb,
        blinkEnabled,
        env,
      ) {
        var self = this;
        this.clock = env.clock;
        this.icon = icon;
        this.finishCallBack = finishCb;
        this.startCooldownCallBack = startCooldownCb;
        this.effectController = new EffectController(
          slot,
          icon,
          effectView,
          splash,
          finishCb,
          function () {
            self.finishFillEffect();
          },
          blinkEnabled,
          env,
        );
        this.cooldownController = new CooldownController(
          slot,
          cooldownView,
          splash,
          function () {
            self.finishCooldown();
          },
          function () {
            self.finishFillCoolDown();
          },
          env,
        );
      }
      var CI = CooldownItem.prototype;
      CI.finishCooldown = function () {
        if (!this.effectController.isActive) this.finishCallBack();
      };
      CI.finishFillEffect = function () {
        this.icon.state = ICON_STATE.EFFECT;
      };
      CI.finishFillCoolDown = function () {
        if (!this.effectController.isActive) this.startCooldownCallBack();
      };
      CI.activateCooldown = function (effectMs, cooldownMs) {
        var now = toInt(this.clock());
        this.effectController.changeTime(now, effectMs, true);
        this.cooldownController.activate(
          now + effectMs,
          cooldownMs,
          this.effectController.isActive,
          false,
        );
      };
      CI.changeEffectTime = function (dur, flag) {
        this.effectController.changeTime(this.clock(), dur, flag);
      };
      CI.activateInfinite = function () {
        this.cooldownController.activateInfinite();
        this.effectController.activateInfinite();
      };
      CI.activateDependedCooldown = function (ms) {
        this.cooldownController.changeTime(
          this.clock(),
          ms,
          this.effectController.isActive,
          true,
        );
      };
      CI.destroy = function () {
        this.effectController.destroy();
        this.cooldownController.destroy();
      };
      CI.canActivate = function () {
        return (
          !this.effectController.isActive && !this.cooldownController.isActive
        );
      };
      CI.isActive = function () {
        return !this.canActivate();
      };
      CI.isEffectActive = function () {
        return this.effectController.isActive;
      };
      CI.isCooldownActive = function () {
        return this.cooldownController.isActive;
      };
      CI.update = function (time) {
        if (
          !this.effectController.isActive &&
          !this.cooldownController.isActive
        )
          return;
        this.effectController.update(time);
        this.cooldownController.update(
          time,
          this.effectController.getProgress(),
        );
      };
      CI.stopEffect = function () {
        this.effectController.stopEffect();
      };
      CI.setCooldownDuration = function (ms) {
        this.cooldownController.setCooldownDuration(ms);
      };
      CI.setCooldownVisible = function (v) {
        this.cooldownController.setVisible(v);
      };

      // ------------------------------------------------------------ light pillar
      var PILLAR_DURATION = 500,
        PILLAR_HEIGHT = 500;

      function LightPillar(clock) {
        this.clock = clock;
        this.startTime = 0;
        this.running = false;
        this.visible = false;
        this.alpha = 1;
        this.color = 0;
        this.h = 0;
        this.y = 0;
        this.completed = 0;
      }
      LightPillar.prototype.init = function (color) {
        this.color = color;
      };
      LightPillar.prototype.start = function () {
        this.running = true;
        this.startTime = this.clock();
        this.visible = true;
        this.alpha = 0;
        this.h = 0;
      };
      LightPillar.prototype.stop = function () {
        this.visible = false;
        this.running = false;
        this.completed++;
      };
      LightPillar.prototype.update = function (now) {
        if (!this.running) return;
        if (now > this.startTime + PILLAR_DURATION) {
          this.stop();
          return;
        }
        var t = (now - this.startTime) / PILLAR_DURATION;
        this.alpha = clamp01(-(2 * t - 1) * (2 * t - 1) + 0.8);
        this.h = toInt(t * PILLAR_HEIGHT);
      };
      LightPillar.prototype.height = PILLAR_HEIGHT;

      // -------------------------------------------------------------- supply slot
      /**
       * `slotNumber` is 1-based (1 first aid, 2 armor, 3 damage, 4 nitro, 5 mine, 6 gold).
       * The slot exposes plain view-state fields; a renderer just reads them.
       */
      function SupplySlot(slotNumber, env) {
        env = env || {};
        this.clock =
          env.clock ||
          function () {
            return 0;
          };
        this.env = {
          clock: this.clock,
          tweens: env.tweens || new TweenManager(),
        };
        this.slotNumber = slotNumber;
        this.lockBits = 0;
        this.flashTimer = 0;
        this.activeAfterDeath = false;
        this.inventoryItem = null;
        this.timers = [];

        // ---- view state (layer order = z-order, bottom first; see renderer)
        this.group = {
          ct: null,
        }; // splash colour transform target
        this.effectView = new SectorView(SLOT_SIZE, "bgd"); // coloured background, wipes with effect time
        this.cooldownView = new SectorView(SLOT_SIZE, "dark"); // dark overlay, wipes with cooldown
        this.icon = {
          state: ICON_STATE.NEUTRAL,
          alpha: 1,
        };
        this.slotNumberLabel = {
          text: String(slotNumber),
          visible: true,
        };
        this.lockIcon = {
          visible: false,
        };
        this.quantityLabel = {
          text: "",
          color: QUANTITY_COLOR_WHITE,
          visible: true,
        };
        this.pillar = new LightPillar(this.clock);
        this.canvasVisible = true;

        this.splash = new Splash(this.group, this.clock);
        var self = this;
        this.cooldownItem = new CooldownItem(
          slotNumber,
          this.effectView,
          this.cooldownView,
          this.splash,
          this.icon,
          function () {
            self.finishEffectAndCooldown();
          },
          function () {
            self.onStartCooldown();
          },
          true,
          this.env,
        );

        // init() ends with: this.setLockMask(1, true)
        this.setLockMask(LOCK.DEAD, true);
      }
      var SP = SupplySlot.prototype;
      SP.setLockMask = function (bit, locked) {
        if (locked) this.lockBits = this.lockBits | bit;
        else {
          this.tryIndicateActivationWithSplash(bit);
          this.lockBits = this.lockBits & ~bit;
        }
        if (bit === 1) {
          if (locked) {
            if (this.slotNumber !== 7) {
              this.icon.state = ICON_STATE.NEUTRAL;
              this.splash.stopFlash();
              this.cooldownItem.destroy();
            }
          } else {
            this.startFlash(this.finishShowStateSplash.bind(this));
          }
        }
        if (locked) this.splash.stopFlash();
        this.updateVisibleSlotNumberAndQuantityLabels();
        this.updateVisibleLockIcon();
      };
      SP.tryIndicateActivationWithSplash = function (bit) {
        var s = this.slotNumber;
        if (
          (s === 1 || s === 5 || s === 6) &&
          this.isLocked() &&
          !this.cooldownItem.isActive() &&
          this.getInventoryCount() > 0 &&
          bit === 2
        ) {
          this.splash.startFlash(SplashColors.getColor(s));
        }
      };
      SP.updateVisibleSlotNumberAndQuantityLabels = function () {
        this.updateVisibleSlotNumberLabel();
        this.updateVisibleQuantityLabel();
      };
      SP.updateVisibleSlotNumberLabel = function () {
        if (this.inventoryItem === null) this.slotNumberLabel.visible = false;
        if (this.isLocked() || this.slotNumber === 7)
          this.slotNumberLabel.visible = false;
        else if (this.cooldownItem.isActive())
          this.slotNumberLabel.visible = false;
        else this.slotNumberLabel.visible = this.getInventoryCount() > 0;
      };
      SP.updateVisibleQuantityLabel = function () {
        if (this.cooldownItem.isActive()) {
          if (
            !this.cooldownItem.isEffectActive() &&
            this.cooldownItem.isCooldownActive()
          )
            this.quantityLabel.color = QUANTITY_COLOR_GRAY;
        } else if (this.isLocked() || this.needLock()) {
          this.quantityLabel.color = QUANTITY_COLOR_GRAY;
        } else {
          this.quantityLabel.color = QUANTITY_COLOR_WHITE;
        }
        this.quantityLabel.visible = !this.needLock();
      };
      SP.updateVisibleLockIcon = function () {
        if (this.isLocked()) {
          this.splash.stopFlash();
          this.lockIcon.visible = true;
        } else {
          this.lockIcon.visible = this.needLock();
          if (!this.cooldownItem.isActive())
            this.icon.state = ICON_STATE.NEUTRAL;
        }
      };
      SP.needLock = function () {
        return this.getInventoryCount() <= 0;
      };
      SP.ready = function () {
        this.cooldownItem.destroy();
        this.icon.state = ICON_STATE.NEUTRAL;
        if (!this.isLocked()) {
          this.startFlash(this.finishShowStateSplash.bind(this));
          this.flashTimer = this.setTimeout(this.startFlash.bind(this), 350);
          this.updateVisibleSlotNumberAndQuantityLabels();
        }
        this.updateVisibleLockIcon();
      };
      SP.finishEffectAndCooldown = function () {
        if (this.cooldownItem.isCooldownActive()) {
          this.icon.state = ICON_STATE.COOLDOWN;
        } else {
          this.icon.state = ICON_STATE.NEUTRAL;
          this.startFlash(this.finishShowStateSplash.bind(this));
          this.quantityLabel.color = QUANTITY_COLOR_WHITE;
          if (this.slotNumber !== 7) this.setLockMask(1, false);
        }
      };
      SP.startFlash = function (cb) {
        this.clearFlashTimer();
        this.splash.startFlash(
          SplashColors.getColor(this.slotNumber),
          typeof cb === "function" ? cb : null,
        );
      };
      SP.onStartCooldown = function () {
        this.updateVisibleQuantityLabel();
        this.icon.state = ICON_STATE.COOLDOWN;
      };
      SP.finishShowStateSplash = function () {
        this.updateVisibleSlotNumberAndQuantityLabels();
      };
      SP.isLocked = function () {
        return this.lockBits !== 0;
      };
      SP.getCanvas = function () {
        return this;
      };
      SP.canActivate = function () {
        return this.cooldownItem.canActivate();
      };
      SP.getInventoryCount = function () {
        return this.inventoryItem === null ? 0 : this.inventoryItem.count;
      };
      SP.setInventoryItem = function (item) {
        if (this.inventoryItem === item) return;
        this.inventoryItem = item;
      };
      SP.updateCounter = function () {
        this.updateVisibleSlotNumberAndQuantityLabels();
        this.updateVisibleLockIcon();
        this.quantityLabel.text = String(this.getInventoryCount());
      };
      SP.setQuantityLabel = function (text) {
        this.quantityLabel.text = text;
      };
      SP.destroy = function () {
        this.clearFlashTimer();
        this.cooldownItem = null;
        this.inventoryItem = null;
      };
      SP.clearFlashTimer = function () {
        if (this.flashTimer !== 0) {
          this.cancelTimeout(this.flashTimer);
          this.flashTimer = 0;
        }
      };
      SP.changeEffectTime = function (ms, flag) {
        this.cooldownItem.changeEffectTime(ms, flag);
        this.updateVisibleSlotNumberAndQuantityLabels();
        this.updateVisibleLockIcon();
      };
      SP.startInfiniteEffect = function () {
        this.cooldownItem.activateInfinite();
      };
      SP.activateCooldown = function (effectMs, cooldownMs) {
        this.cooldownItem.activateCooldown(effectMs, cooldownMs);
        this.updateVisibleSlotNumberAndQuantityLabels();
        this.updateVisibleLockIcon();
      };
      SP.activateDependedCooldown = function (ms) {
        this.cooldownItem.activateDependedCooldown(ms);
        this.updateVisibleSlotNumberAndQuantityLabels();
        this.updateVisibleLockIcon();
      };
      SP.stopEffect = function () {
        this.cooldownItem.stopEffect();
      };
      SP.getSlotNumber = function () {
        return this.slotNumber;
      };
      SP.startReadyIndicator = function () {
        this.pillar.init(getIndicatorColor(this.slotNumber));
        this.pillar.y = -this.pillar.height + SLOT_SIZE - 2;
        this.pillar.start();
      };
      SP.setCooldownDuration = function (ms) {
        this.cooldownItem.setCooldownDuration(ms);
      };
      SP.update = function (now) {
        this.cooldownItem.update(now);
        this.splash.update(now);
        this.pillar.update(now);
        // flash timer (flash.utils.setTimeout) -- fired from the same clock
        for (var i = 0; i < this.timers.length; ) {
          var t = this.timers[i];
          if (now >= t.at) {
            this.timers.splice(i, 1);
            t.fn();
          } else i++;
        }
      };
      var timerId = 0;
      SP.setTimeout = function (fn, ms) {
        var id = ++timerId;
        this.timers.push({
          id: id,
          at: this.clock() + ms,
          fn: fn,
        });
        return id;
      };
      SP.cancelTimeout = function (id) {
        this.timers = this.timers.filter(function (t) {
          return t.id !== id;
        });
      };

      // -------------------------------------------------------------- supply panel
      /**
       * The row of slots. `slots` has 6 entries (index 0..5 = slot numbers 1..6); the 6th
       * (gold box) is created hidden, exactly like initSlots() does.
       */
      function SupplyPanel(opts) {
        opts = opts || {};
        this.clock =
          opts.clock ||
          function () {
            return 0;
          };
        this.tweens = new TweenManager();
        this.env = {
          clock: this.clock,
          tweens: this.tweens,
        };
        this.slots = [];
        this.lockedKeys = {};
        this.uiLockCounter = 0;
        this.keysRegistered = false;
        this.slotIndexByKeyCode = {
          49: 0,
          97: 0,
          50: 1,
          98: 1,
          51: 2,
          99: 2,
          52: 3,
          100: 3,
          53: 4,
          101: 4,
        };
        this.goldKeyCode =
          opts.goldKeyCode === undefined ? 54 : opts.goldKeyCode; // DROP_GOLD_BOX binding
        this.onRequestActivation = opts.onRequestActivation || function () {};
        var cup = !!opts.cupFrozenOrWarmup;
        for (var i = 0; i < 6; i++) {
          var s = new SupplySlot(i + 1, this.env);
          if (cup) s.setLockMask(LOCK.CUP, true);
          if (i === 5) s.canvasVisible = false;
          s.x = 10 + i * (10 + SLOT_SIZE);
          this.slots.push(s);
        }
        this.containerVisible = false;
      }
      var PP = SupplyPanel.prototype;
      PP.assignItemToSlot = function (slotIndex, item) {
        if (item === null || item === undefined) return;
        if (this.getActiveSlotsCount() === 0) this.keysRegistered = true;
        var slot = this.slots[slotIndex];
        slot.inventoryItem = item;
        slot.updateCounter();
        this.containerVisible = true;
      };
      PP.clearSlot = function (slotIndex) {
        var slot = this.slots[slotIndex];
        slot.inventoryItem = null;
        if (this.getActiveSlotsCount() === 0) {
          this.keysRegistered = false;
          this.clearKeyLocks();
        }
      };
      PP.getActiveSlotsCount = function () {
        var n = 0;
        for (var i = 0; i < this.slots.length; i++)
          if (this.slots[i].inventoryItem !== null) n++;
        return n;
      };
      PP.setVisible = function (slotIndex, visible) {
        this.slots[slotIndex].canvasVisible = visible;
      };
      PP.slotsState = function () {
        var s = "";
        for (var i = 0; i < 4 && i < this.slots.length; i++) {
          var c = this.slots[i].cooldownItem;
          if (c !== null)
            s +=
              i +
              1 +
              ":" +
              (c.isEffectActive() ? "1" : "0") +
              (c.isCooldownActive() ? "1" : "0") +
              " ";
        }
        return s;
      };
      PP.changeEffectTime = function (slotIndex, ms, flag, infinite) {
        var slot = this.slots[slotIndex];
        if (!slot) return;
        if (infinite) slot.startInfiniteEffect(flag);
        else slot.changeEffectTime(ms, flag);
      };
      /** Server reply: (effect ms, cooldown ms). Cooldown starts when the effect ends. */
      PP.activateCooldown = function (slotIndex, effectMs, cooldownMs) {
        if (slotIndex >= 0 && slotIndex < this.slots.length)
          this.slots[slotIndex].activateCooldown(effectMs, cooldownMs);
      };
      PP.activateDependedCooldown = function (slotIndex, ms) {
        if (slotIndex >= 0 && slotIndex < this.slots.length)
          this.slots[slotIndex].activateDependedCooldown(ms);
      };
      PP.setCooldownDuration = function (slotIndex, ms) {
        this.slots[slotIndex].setCooldownDuration(ms);
      };
      PP.itemActivated = function (item) {
        for (var i = 0; i < this.slots.length; i++)
          if (this.slots[i].inventoryItem === item)
            this.slots[i].updateCounter();
      };
      PP.lockItem = function (itemId, bits, locked) {
        for (var i = 0; i < this.slots.length; i++) {
          var it = this.slots[i].inventoryItem;
          if (it !== null && it.id === itemId)
            this.slots[i].setLockMask(bits, locked);
        }
      };
      PP.lockItems = function (bits, locked) {
        for (var i = 0; i < this.slots.length; i++)
          this.slots[i].setLockMask(bits, locked);
      };
      PP.killCurrentUser = function () {
        this.lockItems(1, true);
      };
      PP.battleFinish = function () {
        this.lockItems(1, true);
      };
      /** TankEvent 5 with `local` true: the player's tank (re)spawned. */
      PP.localTankSpawned = function () {
        this.lockItems(1, false);
      };
      PP.updateUILock = function (delta) {
        this.uiLockCounter += delta;
        if (this.uiLockCounter < 0) this.uiLockCounter = 0;
        for (var i = 0; i < this.slots.length; i++)
          this.slots[i].setLockMask(4, this.uiLockCounter > 0);
      };
      PP.chatOpened = function () {
        this.updateUILock(1);
      };
      PP.chatClosed = function () {
        this.updateUILock(-1);
      };
      PP.slotIndexFor = function (keyCode) {
        var i = this.slotIndexByKeyCode[keyCode];
        if (i !== undefined) return i;
        if (keyCode === this.goldKeyCode) return 5;
        return null;
      };
      PP.canUseSlot = function (slot) {
        if (slot === null || slot.isLocked()) return false;
        var item = slot.inventoryItem;
        if (item === null || item.count <= 0) return false;
        return slot.canActivate();
      };
      PP.onKey = function (keyCode) {
        if (!this.keysRegistered) return false;
        var now = this.clock();
        var l = this.lockedKeys[keyCode];
        if (l !== undefined && now - l < 30000) return false; // auto-repeat guard (KEY_LOCK_STALE_MS)
        var idx = this.slotIndexFor(keyCode);
        if (idx === null) return false;
        var slot = this.slots[idx];
        if (!this.canUseSlot(slot)) return false;
        this.lockedKeys[keyCode] = now;
        this.onRequestActivation(slot.inventoryItem, idx);
        return true;
      };
      PP.onKeyUp = function (keyCode) {
        delete this.lockedKeys[keyCode];
      };
      PP.clearKeyLocks = function () {
        this.lockedKeys = {};
      };
      /** Per-frame: slots first (panel's enterFrame), then the tween engine. */
      PP.update = function (now) {
        for (var i = 0; i < this.slots.length; i++) this.slots[i].update(now);
        this.tweens.update(now);
      };

      return {
        SLOT: SLOT,
        SLOT_NAMES: SLOT_NAMES,
        ICON_STATE: ICON_STATE,
        LOCK: LOCK,
        QUANTITY_COLOR_WHITE: QUANTITY_COLOR_WHITE,
        QUANTITY_COLOR_GRAY: QUANTITY_COLOR_GRAY,
        SLOT_SIZE: SLOT_SIZE,
        SLOT_PITCH: SLOT_PITCH,
        Blinker: Blinker,
        BlinkConfig: BlinkConfig,
        SplashColors: SplashColors,
        getIndicatorColor: getIndicatorColor,
        Splash: Splash,
        SectorView: SectorView,
        ProgressData: ProgressData,
        TweenManager: TweenManager,
        EffectController: EffectController,
        CooldownController: CooldownController,
        CooldownItem: CooldownItem,
        LightPillar: LightPillar,
        SupplySlot: SupplySlot,
        SupplyPanel: SupplyPanel,
      };
    });

    /*
     * supply-render.js  --  Canvas 2D renderer for SupplyLogic's SupplyPanel.
     *
     * Draws exactly what the SWF's display list would show, bottom to top, per slot:
     *   [light pillar]  (in the slot's canvas, behind the group)
     *   group (optionally 'add' blended; splash ColorTransform applied to the whole group):
     *       1 bgd.png          dark gradient base            (getIcon(10))
     *       2 border.png       2px white rounded frame        (getIcon(12))
     *       3 dark.png         cooldown overlay, pie-masked   (getIcon(14), cooldownView)
     *       4 <name>Bgd.png    coloured background, pie-masked (getBgdIcon(slot), effectView)
     *       5 icon             White / Color / Gray by state, alpha = blink
     *       6 slot number      "1".."6", MyriadPro 10px, bottom-right
     *       7 lock.png         30% black overlay
     *   quantity label  (outside the group): count text with a black GlowFilter
     *
     * Rendering is done at 1:1 logical pixels into an offscreen stage and then blitted with
     * nearest-neighbour scaling, so scaled output stays pixel-exact.
     */
    (function (root, factory) {
      if (typeof module === "object" && module.exports)
        module.exports = factory(require("./supply-logic.js"));
      else root.SupplyRender = factory(root.SupplyLogic);
    })(typeof self !== "undefined" ? self : this, function (SL) {
      "use strict";
      var root = typeof self !== "undefined" ? self : globalThis;
      var SIZE = SL.SLOT_SIZE;
      var NAMES = SL.SLOT_NAMES;

      /**
       * `screenshot`: matches the reference screenshot of the old HUD (normal alpha blending,
       *               10px count text, no dark wipe during an effect, ...). Text knobs were fitted
       *               to that screenshot by tools/compare.html.
       * `swf`:        literal reading of battles.swf (group blendMode 'add', Label default 12px).
       */
      var PROFILES = {
        screenshot: {
          blend: "normal",
          hardMask: true,
          // layer order seen in the screenshot: bgd < dark < lock < border < coloured < icon < number
          borderAboveDark: true,
          lockBelowBorder: true,
          darkDuringEffect: false, // elapsed part of an effect shows plain background, not the dark wipe
          grayIconScale: 0.76, // gray wrench peaks at 97 in the screenshot (asset: 127)
          cooldownNumber: {
            show: true,
            color: 0x7f7f7f,
          }, // "1" stays visible, gray, while an instant item recharges
          quantity: {
            size: 10,
            baseline: -4,
            glowStrength: 0,
            glowAlpha: 0.8,
            glowBlur: 4,
            gray: 0x777777,
            hideDuringEffect: true,
            dx: 0.5,
            dy: 0,
          },
          number: {
            size: 10,
            dx: -0.5,
            dy: 0,
          },
          text: {
            scaleY: 1.04,
            contrast: 1.4,
            bias: 0.2,
          }, // fitted to the screenshot glyphs (tools/compare.html)
        },
        swf: {
          blend: "add",
          hardMask: true,
          borderAboveDark: false,
          lockBelowBorder: false,
          darkDuringEffect: true,
          grayIconScale: 1,
          cooldownNumber: null,
          quantity: {
            size: 12,
            baseline: null,
            glowStrength: 3,
            glowAlpha: 0.8,
            glowBlur: 4,
            gray: null,
            hideDuringEffect: false,
            dx: 0,
            dy: 0,
          },
          number: {
            size: 10,
            dx: 0,
            dy: 0,
          },
          text: {
            scaleY: 1.06,
            contrast: 2.2,
            bias: 0.0,
          },
        },
      };

      function loadImage(src) {
        return new Promise(function (res, rej) {
          var im = new Image();
          im.onload = function () {
            res(im);
          };
          im.onerror = rej;
          im.src = src;
        });
      }

      function mk(w, h) {
        var c = document.createElement("canvas");
        c.width = w;
        c.height = h;
        return c;
      }

      /** Load all slot assets from window.SUPPLY_HUD_ASSETS (data URIs: no CORS / tainting problems). */
      async function loadAssets(dataset) {
        dataset = dataset || root.SUPPLY_HUD_ASSETS;
        var out = {},
          keys = Object.keys(dataset);
        await Promise.all(
          keys.map(async function (k) {
            var im = await loadImage(dataset[k]);
            var c = mk(im.width, im.height),
              x = c.getContext("2d");
            x.drawImage(im, 0, 0);
            out[k] = {
              canvas: c,
              w: im.width,
              h: im.height,
              data: x.getImageData(0, 0, im.width, im.height),
            };
          }),
        );
        return out;
      }

      // ------------------------------------------------------------ pie masks
      /** Path of Pen.drawArc(size/2, size/2, radius, sweep, start, pie=true): 8 quadratic segments. */
      function sectorPath(view) {
        var arc = view.sweepDeg;
        if (arc === 0) return null;
        if (arc > 360) arc = 360;
        var cx = view.size / 2,
          cy = view.size / 2,
          r = view.radius;
        var seg = ((Math.PI / 180) * arc) / 8,
          ctrlR = r / Math.cos(seg / 2);
        var ang = (Math.PI / 180) * view.startDeg;
        var p = new Path2D();
        p.moveTo(cx, cy);
        p.lineTo(cx + Math.cos(ang) * r, cy + Math.sin(ang) * r);
        for (var i = 0; i < 8; i++) {
          ang += seg;
          p.quadraticCurveTo(
            cx + Math.cos(ang - seg / 2) * ctrlR,
            cy + Math.sin(ang - seg / 2) * ctrlR,
            cx + Math.cos(ang) * r,
            cy + Math.sin(ang) * r,
          );
        }
        p.lineTo(cx, cy);
        p.closePath();
        return p;
      }

      // -------------------------------------------------------------- text
      /** Glyph outline run -> Path2D, in px, baseline at y=0. */
      function textPath(text, size, scaleY) {
        var F = root.SUPPLY_HUD_FONT,
          em = F._meta.em,
          k = size / em,
          pen = 0,
          p = new Path2D();
        for (var i = 0; i < text.length; i++) {
          var g = F[text[i]];
          if (!g) continue;
          var ops = g.path;
          for (var j = 0; j < ops.length; j++) {
            var o = ops[j];
            if (o[0] === "M") {
              if (j) p.closePath();
              p.moveTo(pen + o[1] * k, o[2] * k * scaleY);
            } else if (o[0] === "L")
              p.lineTo(pen + o[1] * k, o[2] * k * scaleY);
            else
              p.quadraticCurveTo(
                pen + o[1] * k,
                o[2] * k * scaleY,
                pen + o[3] * k,
                o[4] * k * scaleY,
              );
          }
          p.closePath();
          pen += g.adv * k;
        }
        return {
          path: p,
          width: pen,
        };
      }

      function textWidth(text, size) {
        var F = root.SUPPLY_HUD_FONT,
          w = 0;
        for (var i = 0; i < text.length; i++)
          if (F[text[i]]) w += F[text[i]].adv;
        return (w * size) / F._meta.em;
      }

      var EMPTY_VIEW = {
        visible: true,
        sweepDeg: 0,
      };
      var scratch = null;
      /**
       * Draw `text` as crisp, hinted-looking glyphs. Renders the outline with AA into a scratch alpha
       * buffer, then sharpens coverage (contrast about 50%) to imitate Flash's gridfit/"advanced" AA
       * with sharpness 150 / thickness 70. Returns an Uint8ClampedArray alpha mask at (x0,y0,w,h).
       */
      function rasterText(text, size, penX, baseY, T, boxW, boxH, offX, offY) {
        if (!scratch || scratch.width < boxW || scratch.height < boxH)
          scratch = mk(Math.max(boxW, 64), Math.max(boxH, 64));
        var c = scratch,
          x = c.getContext("2d", {
            willReadFrequently: true,
          });
        x.setTransform(1, 0, 0, 1, 0, 0);
        x.clearRect(0, 0, c.width, c.height);
        var tp = textPath(text, size, T.scaleY);
        x.setTransform(1, 0, 0, 1, penX - offX, baseY - offY);
        x.fillStyle = "#fff";
        x.fill(tp.path);
        x.setTransform(1, 0, 0, 1, 0, 0);
        var d = x.getImageData(0, 0, boxW, boxH).data,
          a = new Float32Array(boxW * boxH);
        for (var i = 0; i < a.length; i++) {
          var v = d[i * 4 + 3] / 255;
          a[i] = Math.max(
            0,
            Math.min(1, (v - 0.5) * T.contrast + 0.5 + T.bias),
          );
        }
        return a;
      }

      function boxBlur(src, w, h, n) {
        // Flash box blur of width n: tap window [-n/2, n/2-1]
        var tmp = new Float32Array(w * h),
          out = new Float32Array(w * h),
          lo = -Math.floor(n / 2),
          hi = lo + n - 1,
          y,
          x,
          k,
          s;
        for (y = 0; y < h; y++)
          for (x = 0; x < w; x++) {
            s = 0;
            for (k = lo; k <= hi; k++) {
              var xx = x + k;
              if (xx >= 0 && xx < w) s += src[y * w + xx];
            }
            tmp[y * w + x] = s / n;
          }
        for (y = 0; y < h; y++)
          for (x = 0; x < w; x++) {
            s = 0;
            for (k = lo; k <= hi; k++) {
              var yy = y + k;
              if (yy >= 0 && yy < h) s += tmp[yy * w + x];
            }
            out[y * w + x] = s / n;
          }
        return out;
      }

      // ----------------------------------------------------------- the renderer
      /**
       * @param {HTMLCanvasElement} canvas  visible canvas
       * @param {SupplyPanel} panel
       * @param {object} assets  from loadAssets()
       * @param {object} opts    { width, height, originX, originY, scale, profile, background(ctx,w,h) }
       */
      function SupplyHudRenderer(canvas, panel, assets, opts) {
        opts = opts || {};
        this.canvas = canvas;
        this.panel = panel;
        this.assets = assets;
        this.width = opts.width || 360;
        this.height = opts.height || 120;
        this.originX = opts.originX === undefined ? 31 : opts.originX; // slot 1 sits at 10 + 31 = 41 in the screenshot
        this.originY = opts.originY === undefined ? 57 : opts.originY;
        this.scale = opts.scale || 1;
        this.setProfile(opts.profile || "screenshot");
        this.background = opts.background || null;
        this.stage = mk(this.width, this.height);
        this.sctx = this.stage.getContext("2d", {
          willReadFrequently: true,
        });
        this.group = mk(SIZE, SIZE);
        this.gctx = this.group.getContext("2d", {
          willReadFrequently: true,
        });
        this.layer = mk(SIZE, SIZE);
        this.lctx = this.layer.getContext("2d", {
          willReadFrequently: true,
        });
        this.pathCtx = mk(1, 1).getContext("2d");
        this.maskCache = new WeakMap();
        this.tintCache = {};
        this.canvas.width = this.width * this.scale;
        this.canvas.height = this.height * this.scale;
        this.cctx = this.canvas.getContext("2d");
      }
      var R = SupplyHudRenderer.prototype;
      R.setProfile = function (p) {
        this.profileName = typeof p === "string" ? p : "custom";
        this.profile =
          typeof p === "string" ? JSON.parse(JSON.stringify(PROFILES[p])) : p;
      };

      /** 40x40 coverage mask (1 = inside) of a SectorView; cached until its version changes. */
      R.maskFor = function (view) {
        var c = this.maskCache.get(view);
        if (c && c.version === view.version) return c;
        var m = new Uint8Array(SIZE * SIZE),
          path = sectorPath(view);
        if (path)
          for (var y = 0; y < SIZE; y++)
            for (var x = 0; x < SIZE; x++)
              m[y * SIZE + x] = this.pathCtx.isPointInPath(
                path,
                x + 0.5,
                y + 0.5,
              )
                ? 1
                : 0;
        c = {
          version: view.version,
          mask: m,
          path: path,
        };
        this.maskCache.set(view, c);
        return c;
      };
      R.drawMasked = function (ctx, asset, view) {
        if (!view.visible || view.sweepDeg === 0) return;
        var m = this.maskFor(view);
        if (!m.path) return;
        if (this.profile.hardMask) {
          var src = asset.data.data,
            out = this.lctx.createImageData(SIZE, SIZE),
            o = out.data;
          for (var i = 0; i < SIZE * SIZE; i++)
            if (m.mask[i]) {
              o[i * 4] = src[i * 4];
              o[i * 4 + 1] = src[i * 4 + 1];
              o[i * 4 + 2] = src[i * 4 + 2];
              o[i * 4 + 3] = src[i * 4 + 3];
            }
          this.lctx.putImageData(out, 0, 0);
          ctx.drawImage(this.layer, 0, 0);
        } else {
          ctx.save();
          ctx.clip(m.path);
          ctx.drawImage(asset.canvas, 0, 0);
          ctx.restore();
        }
      };

      R.iconAsset = function (slot) {
        var n = NAMES[slot.slotNumber],
          st = slot.icon.state;
        return this.assets[
          n + (st === 0 ? "WhiteIcon" : st === 1 ? "ColorIcon" : "GrayIcon")
        ];
      };

      /** Draw a slot number label inside the group (bottom-right, 2px from the edge). */
      R.drawNumber = function (ctx, text, color) {
        color = color === undefined ? 0xffffff : color;
        var cr = (color >> 16) & 255,
          cg = (color >> 8) & 255,
          cb = color & 255;
        var size = this.profile.number.size,
          asc = Math.ceil((size * 18200) / 20480),
          desc = Math.ceil((size * 5120) / 20480);
        var w = textWidth(text, size) + 4,
          h = asc + desc + 4;
        var N = this.profile.number,
          x = SIZE - w - 2,
          y = SIZE - h,
          penX = x + 2 + (N.dx || 0),
          base = y + 2 + asc + (N.dy || 0);
        var a = rasterText(
          text,
          size,
          penX,
          base,
          this.profile.text,
          SIZE,
          SIZE,
          0,
          0,
        );
        var img = ctx.getImageData(0, 0, SIZE, SIZE),
          d = img.data;
        for (var i = 0; i < a.length; i++)
          if (a[i] > 0) {
            var al = a[i],
              j = i * 4,
              da = d[j + 3] / 255,
              oa = al + da * (1 - al);
            var cc = [cr, cg, cb];
            for (var k = 0; k < 3; k++)
              d[j + k] = oa ? (cc[k] * al + d[j + k] * da * (1 - al)) / oa : 0;
            d[j + 3] = oa * 255;
          }
        ctx.putImageData(img, 0, 0);
      };

      R.applyColorTransform = function (ctx, ct) {
        if (
          !ct ||
          (ct.rm === 1 &&
            ct.gm === 1 &&
            ct.bm === 1 &&
            ct.am === 1 &&
            !ct.ro &&
            !ct.go &&
            !ct.bo &&
            !ct.ao)
        )
          return;
        var img = ctx.getImageData(0, 0, SIZE, SIZE),
          d = img.data;

        function cl(v) {
          return v < 0 ? 0 : v > 255 ? 255 : v;
        }
        for (var i = 0; i < d.length; i += 4) {
          d[i] = cl(d[i] * ct.rm + ct.ro);
          d[i + 1] = cl(d[i + 1] * ct.gm + ct.go);
          d[i + 2] = cl(d[i + 2] * ct.bm + ct.bo);
          d[i + 3] = cl(d[i + 3] * ct.am + ct.ao);
        }
        ctx.putImageData(img, 0, 0);
      };

      R.renderGroup = function (slot) {
        var g = this.gctx,
          A = this.assets;
        g.setTransform(1, 0, 0, 1, 0, 0);
        g.clearRect(0, 0, SIZE, SIZE);
        g.globalAlpha = 1;
        g.globalCompositeOperation = "source-over";
        var P = this.profile,
          ci = slot.cooldownItem;
        var darkView =
          !P.darkDuringEffect && ci && ci.isEffectActive()
            ? EMPTY_VIEW
            : slot.cooldownView;
        g.drawImage(A.bgd.canvas, 0, 0);
        if (P.lockBelowBorder) {
          // reference screenshot order
          this.drawMasked(g, A.dark, darkView);
          if (slot.lockIcon.visible) g.drawImage(A.lock.canvas, 0, 0);
          g.drawImage(A.border.canvas, 0, 0);
        } else if (P.borderAboveDark) {
          this.drawMasked(g, A.dark, darkView);
          g.drawImage(A.border.canvas, 0, 0);
        } else {
          // battles.swf addChild order
          g.drawImage(A.border.canvas, 0, 0);
          this.drawMasked(g, A.dark, darkView);
        }
        this.drawMasked(g, A[NAMES[slot.slotNumber] + "Bgd"], slot.effectView);
        g.globalAlpha =
          slot.icon.alpha * (slot.icon.state === 2 ? P.grayIconScale : 1);
        g.drawImage(this.iconAsset(slot).canvas, 0, 0);
        g.globalAlpha = 1;
        var showNum = slot.slotNumberLabel.visible,
          numColor = 0xffffff;
        if (
          !showNum &&
          P.cooldownNumber &&
          P.cooldownNumber.show &&
          ci &&
          ci.isCooldownActive() &&
          !ci.isEffectActive() &&
          slot.getInventoryCount() > 0
        ) {
          showNum = true;
          numColor = P.cooldownNumber.color;
        }
        if (showNum) this.drawNumber(g, slot.slotNumberLabel.text, numColor);
        if (!P.lockBelowBorder && slot.lockIcon.visible)
          g.drawImage(A.lock.canvas, 0, 0);
        this.applyColorTransform(g, slot.group.ct);
        return this.group;
      };

      R.tinted = function (name, color) {
        var key = name + ":" + color;
        if (this.tintCache[key]) return this.tintCache[key];
        var a = this.assets[name],
          c = mk(a.w, a.h),
          x = c.getContext("2d");
        x.drawImage(a.canvas, 0, 0);
        x.globalCompositeOperation = "source-in";
        x.fillStyle = "#" + ("000000" + color.toString(16)).slice(-6);
        x.fillRect(0, 0, a.w, a.h);
        return (this.tintCache[key] = c);
      };
      R.drawPillar = function (ctx, slot) {
        var p = slot.pillar;
        if (!p.visible || p.alpha <= 0 || p.h <= 0) return;
        var h = p.h,
          x = this.originX + slot.x,
          y0 = this.originY + p.y + (p.height - h);
        var top = this.tinted("pillar_top", p.color),
          bar = this.tinted("pillar_bar", p.color);
        var th = this.assets.pillar_top.h;
        ctx.save();
        ctx.globalAlpha = p.alpha;
        ctx.drawImage(
          top,
          0,
          0,
          SIZE,
          Math.min(h, th),
          x,
          y0,
          SIZE,
          Math.min(h, th),
        );
        if (h > th) ctx.drawImage(bar, 0, 0, SIZE, 1, x, y0 + th, SIZE, h - th);
        ctx.restore();
      };

      R.drawQuantity = function (ctx, slot) {
        var q = slot.quantityLabel,
          P = this.profile.quantity;
        if (!q.visible || q.text === "") return;
        if (
          P.hideDuringEffect &&
          slot.cooldownItem &&
          slot.cooldownItem.isEffectActive()
        )
          return;
        var size = P.size,
          asc = Math.ceil((size * 18200) / 20480),
          desc = Math.ceil((size * 5120) / 20480);
        var w = textWidth(q.text, size) + 4;
        var x = this.originX + slot.x + (SIZE - w) / 2,
          penX = x + 2 + (P.dx || 0);
        var base =
          this.originY +
          (P.baseline !== null ? P.baseline : -desc - 4) +
          (P.dy || 0);
        // box covering text + glow bleed
        var pad = 8,
          bx = Math.floor(penX) - pad,
          by = Math.floor(base - asc) - pad,
          bw = Math.ceil(w) + pad * 2,
          bh = asc + desc + pad * 2;
        var a = rasterText(
          q.text,
          size,
          penX,
          base,
          this.profile.text,
          bw,
          bh,
          bx,
          by,
        );
        var glow = boxBlur(a, bw, bh, P.glowBlur),
          col = q.color;
        if (
          P.gray !== null &&
          P.gray !== undefined &&
          col === SL.QUANTITY_COLOR_GRAY
        )
          col = P.gray;
        var r = (col >> 16) & 255,
          g = (col >> 8) & 255,
          b = col & 255;
        var img = ctx.getImageData(bx, by, bw, bh),
          d = img.data;
        for (var i = 0; i < a.length; i++) {
          var ga = Math.min(1, glow[i] * P.glowStrength) * P.glowAlpha,
            j = i * 4;
          if (ga > 0) {
            d[j] = d[j] * (1 - ga);
            d[j + 1] = d[j + 1] * (1 - ga);
            d[j + 2] = d[j + 2] * (1 - ga);
          }
          var al = a[i];
          if (al > 0) {
            d[j] = d[j] * (1 - al) + r * al;
            d[j + 1] = d[j + 1] * (1 - al) + g * al;
            d[j + 2] = d[j + 2] * (1 - al) + b * al;
          }
        }
        ctx.putImageData(img, bx, by);
      };

      /** Draw one frame at logical time `now` (call panel.update(now) first). */
      R.render = function () {
        var s = this.sctx,
          add = this.profile.blend === "add";
        s.setTransform(1, 0, 0, 1, 0, 0);
        s.globalCompositeOperation = "source-over";
        s.globalAlpha = 1;
        s.clearRect(0, 0, this.width, this.height);
        if (this.background) this.background(s, this.width, this.height);
        var slots = this.panel.slots,
          i,
          slot;
        for (i = 0; i < slots.length; i++) {
          slot = slots[i];
          if (!slot.canvasVisible) continue;
          this.drawPillar(s, slot);
          var grp = this.renderGroup(slot);
          s.globalCompositeOperation = add ? "lighter" : "source-over";
          s.drawImage(grp, this.originX + slot.x, this.originY);
          s.globalCompositeOperation = "source-over";
          this.drawQuantity(s, slot);
        }
        var c = this.cctx;
        c.imageSmoothingEnabled = false;
        c.clearRect(0, 0, this.canvas.width, this.canvas.height);
        c.drawImage(
          this.stage,
          0,
          0,
          this.width * this.scale,
          this.height * this.scale,
        );
      };

      return {
        SupplyHudRenderer: SupplyHudRenderer,
        loadAssets: loadAssets,
        PROFILES: PROFILES,
        sectorPath: sectorPath,
        textWidth: textWidth,
        boxBlur: boxBlur,
      };
    });

    /* ---------------------------------------------------------------- convenience wrapper */
    (function (root) {
      "use strict";
      /**
       * SupplyHud.create(canvas, opts) -> Promise<hud>
       *   opts: { counts:[5 numbers], zoom:3, look:'screenshot'|'swf', background:(ctx,w,h)=>void,
       *           onRequestActivation:(slotIndex, item)=>void }   // key pressed -> ask your server
       *   hud.activate(slotIndex, effectMs, cooldownMs)   // server reply (slotIndex 0..4, 5 = gold box)
       *   hud.setCount(slotIndex, n)   hud.spawn()   hud.die()   hud.panel   hud.destroy()
       */
      root.SupplyHud = {
        create: async function (canvas, opts) {
          opts = opts || {};
          var assets = await root.SupplyRender.loadAssets();
          var panel = new root.SupplyLogic.SupplyPanel({
            clock: function () {
              return performance.now();
            },
            onRequestActivation: function (item, idx) {
              if (opts.onRequestActivation) opts.onRequestActivation(idx, item);
            },
          });
          var items = [];
          for (var i = 0; i < 6; i++) {
            items[i] = {
              id: i + 1,
              count: (opts.counts && opts.counts[i]) || 0,
            };
            if (i < 5 || items[i].count) panel.assignItemToSlot(i, items[i]);
          }
          var rend = new root.SupplyRender.SupplyHudRenderer(
            canvas,
            panel,
            assets,
            {
              width: opts.width || 360,
              height: opts.height || 120,
              originX: 31,
              originY: 57,
              scale: opts.zoom || 1,
              profile: opts.look || "screenshot",
              background: opts.background || null,
            },
          );
          var raf = 0,
            dead = false;

          function frame() {
            if (dead) return;
            panel.update(performance.now());
            rend.render();
            raf = requestAnimationFrame(frame);
          }

          function kd(e) {
            panel.onKey(e.keyCode);
          }

          function ku(e) {
            panel.onKeyUp(e.keyCode);
          }
          addEventListener("keydown", kd);
          addEventListener("keyup", ku);
          panel.localTankSpawned();
          frame();
          return {
            panel: panel,
            renderer: rend,
            items: items,
            activate: function (idx, effectMs, cooldownMs) {
              items[idx].count = Math.max(0, items[idx].count - 1);
              panel.itemActivated(items[idx]);
              panel.activateCooldown(idx, effectMs, cooldownMs);
            },
            setCount: function (idx, n) {
              items[idx].count = n;
              panel.slots[idx].updateCounter();
            },
            spawn: function () {
              panel.localTankSpawned();
            },
            die: function () {
              panel.killCurrentUser();
            },
            destroy: function () {
              dead = true;
              cancelAnimationFrame(raf);
              removeEventListener("keydown", kd);
              removeEventListener("keyup", ku);
            },
          };
        },
      };
    })(typeof self !== "undefined" ? self : this);
    return self;
  })();

  const CHUD = (function () {
    const SIZE = 40,
      PAD = 10,
      SW = 300,
      TOP_LOW = 60,
      TOP_BEAM = 470; // drawing area in game pixels: the count sits just above the slots; the ready beam rises about 460 px, so the area is only that tall while a beam is showing
    let S = null,
      loading = false,
      raf = 0,
      fails = 0,
      lastErr = "",
      alive = false,
      assetsP = null;

    // The cooldown pie of the game's slot is an SVG polygon on a 36x36 square: its first point is where the dark sector starts
    // (clockwise from 12 o'clock), then come the corners. Returns that start as 0..1, or null. 1 = nothing left (cooldown over).
    const pieStart = (pts) => {
      const m = String(pts == null ? "" : pts)
        .trim()
        .split(/\s+/);
      if (m.length < 3) return null;
      const f = m[0].split(",").map(Number);
      if (!isFinite(f[0]) || !isFinite(f[1])) return null;
      const a = Math.atan2(f[0] - 18, 18 - f[1]) / (2 * Math.PI),
        u = a < 0 ? a + 1 : a;
      return u < 0.0005 && m.length <= 4 ? 1 : u;
    };

    function build(assets) {
      const SL = SUPPLY_LIB.SupplyLogic,
        SR = SUPPLY_LIB.SupplyRender;
      const canvas = document.createElement("canvas");
      canvas.id = "__classic_supplies";
      canvas.dataset.pfxOverlay = "1";
      canvas.style.cssText =
        "position:fixed;left:0;top:0;pointer-events:none;z-index:18;image-rendering:pixelated;display:none;";
      const panel = new SL.SupplyPanel({
        clock: () => performance.now(),
      });
      for (let i = 0; i < 5; i++)
        panel.assignItemToSlot(i, {
          id: i + 1,
          count: 0,
        });
      panel.slots[5].canvasVisible = false; // the gold box slot is not part of this bar
      for (const s of panel.slots) {
        s.lockBits = 0;
        s._cd = false;
        s._has = false;
        s._init = false;
        s._q = null;
        // the library draws from its own timers; here the state comes from the game, so tell it what the game shows
        const ci = s.cooldownItem;
        ci.isCooldownActive = () => s._cd;
        ci.isEffectActive = () => ci.effectController.isActive;
      }
      const ren = new SR.SupplyHudRenderer(canvas, panel, assets, {
        width: SW,
        height: TOP_LOW + SIZE + 10,
        originX: PAD,
        originY: TOP_LOW,
        scale: 1,
        profile: "screenshot",
      });
      // Pie masks: the library tests all 1600 pixels of a slot against a path every time a pie moves. A pie is a wedge, so an angle test gives
      // the same answer; only pixels right on an edge are still asked the path (so the result is identical). A mask also carries a counter
      // that only goes up when its pixels really changed, so frames where the pie moved by less than a pixel need no redraw.
      const ANG = new Float64Array(SIZE * SIZE);
      for (let y = 0; y < SIZE; y++)
        for (let x = 0; x < SIZE; x++)
          ANG[y * SIZE + x] = Math.atan2(
            y + 0.5 - SIZE / 2,
            x + 0.5 - SIZE / 2,
          );
      const TAU = 2 * Math.PI,
        EPS = 1e-6;
      ren.maskFor = function (view) {
        const old = this.maskCache.get(view);
        if (old && old.version === view.version) return old;
        const m = new Uint8Array(SIZE * SIZE),
          path = SR.sectorPath(view);
        if (path) {
          const sweep = (Math.min(view.sweepDeg, 360) * Math.PI) / 180,
            st = (view.startDeg * Math.PI) / 180;
          for (let i = 0; i < m.length; i++) {
            if (sweep >= TAU) {
              m[i] = 1;
              continue;
            } // a full circle covers the whole square
            let rel = (ANG[i] - st) % TAU;
            if (rel < 0) rel += TAU;
            if (rel < EPS || rel > TAU - EPS || Math.abs(rel - sweep) < EPS)
              m[i] = this.pathCtx.isPointInPath(
                path,
                (i % SIZE) + 0.5,
                ((i / SIZE) | 0) + 0.5,
              )
                ? 1
                : 0;
            else m[i] = rel < sweep ? 1 : 0;
          }
        }
        let mv = old ? old.mv : 0;
        if (
          !old ||
          old.mask.length !== m.length ||
          old.mask.some((v, i) => v !== m[i])
        )
          mv++;
        const e = {
          version: view.version,
          mask: m,
          path,
          mv,
        };
        this.maskCache.set(view, e);
        return e;
      };
      // slot numbers never change: draw each one once and paste it
      const origNumber = ren.drawNumber,
        numCache = new Map();
      ren.drawNumber = function (ctx, text, color) {
        const key = text + "|" + color;
        let c = numCache.get(key);
        if (!c) {
          c = document.createElement("canvas");
          c.width = c.height = SIZE;
          origNumber.call(
            this,
            c.getContext("2d", {
              willReadFrequently: true,
            }),
            text,
            color,
          );
          numCache.set(key, c);
        }
        ctx.drawImage(c, 0, 0);
      };
      // the count above each slot is not rendered again: the game's own text bitmap is copied (grey while recharging)
      const tint = document.createElement("canvas");
      ren.drawQuantity = function (ctx, slot) {
        const q = slot._q;
        if (!q || !slot._has || slot.cooldownItem.isEffectActive()) return; // like the old bar, no count while an effect runs
        const w = q.offsetWidth || q.width,
          h = q.offsetHeight || q.height;
        if (!(w > 0 && h > 0)) return;
        const x = Math.round(this.originX + slot.x + (SIZE - w) / 2),
          y = this.originY - 4 - Math.round((h * 11) / 16); // the digits' baseline is at 11/16 of the bitmap
        if (slot._cd) {
          tint.width = w;
          tint.height = h;
          const t = tint.getContext("2d");
          t.clearRect(0, 0, w, h);
          t.globalCompositeOperation = "source-over";
          t.drawImage(q, 0, 0, w, h);
          t.globalCompositeOperation = "source-atop";
          t.fillStyle = "#777";
          t.fillRect(0, 0, w, h);
          ctx.drawImage(tint, x, y);
        } else ctx.drawImage(q, x, y, w, h);
      };
      (document.body || document.documentElement).append(canvas);
      return {
        canvas,
        panel,
        ren,
        scale: 1,
        top: TOP_LOW,
        last: 0,
        key: "",
        sig: "",
      };
    }

    // fingerprint of the count text bitmap: a supply really was used when it changes together with a new cooldown pie
    // (the game also shows a pie right after spawning, which must not start an effect)
    const fp = (q, has) => {
      if (!has || !q) return "none";
      try {
        const d = q.getContext("2d").getImageData(0, 0, q.width, q.height).data;
        let a = 0,
          b = 0;
        for (let i = 3; i < d.length; i += 4) {
          a += d[i];
          b = (b * 31 + d[i]) | 0;
        }
        return a + ":" + b;
      } catch (e) {
        return "x";
      }
    };

    function hide() {
      if (S) {
        if (S.canvas.style.display !== "none") S.canvas.style.display = "none";
        for (const s of S.panel.slots) {
          s._init = false;
          s.cooldownItem.effectController.destroy();
        }
      }
      alive = false;
    }

    function step(now) {
      const p = document.querySelector("app-inventory-panel");
      const els = p ? p.querySelectorAll("app-inventory-slot") : null;
      if (!els || els.length < 5 || !(els[0].offsetWidth > 0)) {
        hide();
        return;
      }
      const r0 = els[0].getBoundingClientRect(),
        k = r0.width / SIZE;
      if (!(k > 0.05)) {
        hide();
        return;
      }
      const panel = S.panel,
        ren = S.ren,
        canvas = S.canvas;
      for (let i = 0; i < 5; i++) {
        const g = els[i],
          s = panel.slots[i];
        const shown = !g.hidden && g.offsetWidth > 0;
        s.canvasVisible = shown;
        if (!shown) {
          s._init = false;
          continue;
        }
        s.x = (g.getBoundingClientRect().left - r0.left) / k;
        const img = g.querySelector("img.inventory-icon");
        const has =
          !!img && !/\/empty\.png/.test(img.getAttribute("src") || ""); // the game shows "empty" when none are left
        const poly = g.querySelector("svg.cooldown-mask polygon");
        const a = poly ? pieStart(poly.getAttribute("points")) : null;
        const cd = a !== null && a < 0.999;
        s._q = g.querySelector("flash-label.quantity-label canvas");
        if (!s._hist) {
          s._hist = [];
          s._hT = -1e9;
          s._cur = "";
          s._pend = 0;
        }
        if (now - s._hT >= 100) {
          s._hT = now;
          s._cur = fp(s._q, has);
          s._hist.push([now, s._cur]);
          while (s._hist.length > 12) s._hist.shift();
        }
        const fx = i >= 1 && i <= 3 && HUD.effectTime > 0; // armor, damage and nitro (slots 2-4) also run an effect once used
        if (s._init) {
          // things that just happened: cooldown started, recharged, restocked
          if (cd && !s._cd) {
            if (fx) {
              let base = s._hist[0];
              for (const h of s._hist) if (h[0] <= now - 300) base = h; // what the count looked like just before
              s._h0 = base ? base[1] : s._cur;
              s._pend = now;
            } else s.startFlash();
          } else if (!cd && s._cd && has && !s.cooldownItem.isEffectActive()) {
            s.startFlash();
            if (HUD.beam) s.startReadyIndicator();
          } else if (has && !s._has) s.startFlash();
        }
        if (s._pend) {
          if (!cd) s._pend = 0;
          else if (s._cur !== s._h0) {
            s._pend = 0;
            s.cooldownItem.effectController.changeTime(
              performance.now(),
              HUD.effectTime * 1000,
              true,
            );
          } else if (now - s._pend > 800) s._pend = 0;
        }
        s._init = true;
        s._cd = cd;
        s._has = has;
        s.inventoryItem.count = has ? 1 : 0;
        s.cooldownView.setProgress(cd ? a : 0, cd ? 1 : 0);
        const eff = s.cooldownItem.isEffectActive();
        if (!eff) s.icon.state = cd ? 2 : 0; // while an effect runs the library sets the coloured icon itself
        s.lockIcon.visible = !has;
        s.slotNumberLabel.visible = has && !cd && !eff;
        s.quantityLabel.visible = has;
      }
      panel.update(now);
      let beam = false;
      for (const s of panel.slots)
        if (s.pillar.running) {
          beam = true;
          break;
        }
      const top = beam ? TOP_BEAM : S.top === TOP_BEAM ? TOP_BEAM : TOP_LOW,
        dpr = window.devicePixelRatio || 1,
        sc = Math.max(1, Math.round(k * dpr));
      let resized = false;
      if (!beam && S.top === TOP_BEAM) {
        S.top = TOP_LOW;
        resized = true;
      } else if (beam && S.top !== TOP_BEAM) {
        S.top = TOP_BEAM;
        resized = true;
      }
      if (sc !== S.scale) {
        S.scale = sc;
        resized = true;
      }
      if (resized) {
        const h = S.top + SIZE + 10;
        S.scale = sc;
        ren.scale = sc;
        ren.originY = S.top;
        ren.height = h;
        ren.stage.width = SW;
        ren.stage.height = h;
        canvas.width = SW * sc;
        canvas.height = h * sc;
        S.sig = "";
      }
      const key = [r0.left, r0.top, k, S.top].join();
      if (key !== S.key) {
        S.key = key;
        Object.assign(canvas.style, {
          left: Math.round(r0.left - PAD * k) + "px",
          top: Math.round(r0.top - S.top * k) + "px",
          width: Math.round(SW * k) + "px",
          height: Math.round((S.top + SIZE + 10) * k) + "px",
        });
      }
      if (canvas.style.display !== "") canvas.style.display = "";
      // draw only when something visible changed (cooldown/effect progress, flashes, beam, icons), and now and then to pick up a changed count
      let sig = "";
      for (const s of panel.slots) {
        const ct = s.group.ct;
        sig +=
          ren.maskFor(s.cooldownView).mv +
          (s._has ? "h" : "n") +
          ren.maskFor(s.effectView).mv +
          "," +
          s.icon.state +
          "," +
          Math.round(s.icon.alpha * 20) +
          "," +
          (ct ? ct.rm.toFixed(2) : "") +
          "," +
          (s.pillar.running ? Math.round(now / 16) : "") +
          "," +
          Math.round(s.x) +
          ";";
      }
      if (sig !== S.sig || now - S.last > 250) {
        S.sig = sig;
        ren.render();
        S.last = now;
      }
      alive = true;
    }

    function frame() {
      if (!S) return;
      raf = requestAnimationFrame(frame);
      if (!HUD.classic) {
        stop();
        return;
      }
      try {
        step(performance.now());
        fails = 0;
      } catch (e) {
        lastErr = String((e && e.stack) || e);
        alive = false;
        if (++fails > 30) {
          console.error("[postfx] classic supplies stopped:", lastErr);
          HUD.classic = false;
          stop();
        }
      }
    }

    function load() {
      loading = true;
      (assetsP || (assetsP = SUPPLY_LIB.SupplyRender.loadAssets()))
        .then((assets) => {
          loading = false;
          if (!HUD.classic || S) return;
          S = build(assets);
          fails = 0;
          raf = requestAnimationFrame(frame);
        })
        .catch((e) => {
          loading = false;
          assetsP = null;
          lastErr = String(e);
          HUD.classic = false;
          console.error(
            "[postfx] classic supplies: the artwork could not be loaded",
            e,
          );
        });
    }

    function stop() {
      cancelAnimationFrame(raf);
      raf = 0;
      if (S) {
        S.canvas.remove();
        S = null;
      }
      alive = false;
      try {
        hudApply();
      } catch (e) {}
    }

    // called regularly and whenever the switch is flipped
    function sync() {
      if (HUD.classic) {
        if (!S && !loading) load();
      } else if (S) stop();
    }

    return {
      sync,
      stop,
      get alive() {
        return alive;
      },
      get error() {
        return lastErr;
      },
      get state() {
        return S;
      },
    };
  })();

  const hudStyle = document.createElement("style");
  hudStyle.id = "postfx-hud";
  let hudCss = "";
  const hudApply = () => {
    try {
      CHUD.sync();
    } catch (e) {
      console.error("[postfx] classic supplies", e);
    }
    let css = "";
    const sc = Math.abs(HUD.scale - 1) > 0.001 ? HUD.scale : 1;
    const p =
      HUD.center || sc !== 1
        ? document.querySelector("app-inventory-panel")
        : null;
    if (p) {
      // the panel is wider than the icons in it (room for more slots on the right), so it is the icons that get measured, not the panel.
      // offsetLeft/offsetWidth are used because they ignore the scaling that is applied below.
      let a = Infinity,
        b = -Infinity;
      for (const s of p.querySelectorAll("app-inventory-slot")) {
        if (!(s.offsetWidth > 0)) continue;
        const o =
          s.offsetLeft +
          (s.offsetParent && s.offsetParent !== p
            ? s.offsetParent.offsetLeft
            : 0);
        a = Math.min(a, o);
        b = Math.max(b, o + s.offsetWidth);
      }
      if (b > a) {
        const vw =
            (p.parentElement && p.parentElement.clientWidth) ||
            window.innerWidth,
          cx = HUD.center ? (a + b) / 2 : a;
        let r = "transform-origin:" + cx + "px 100% !important;"; // grow around the middle of the icons (or their left edge) and keep the bottom edge where it is
        if (sc !== 1) r += "transform:scale(" + sc + ") !important;";
        if (HUD.center)
          r +=
            "left:" +
            Math.round(vw / 2 - cx) +
            "px !important;right:auto !important;"; // whole pixels, so the icons stay sharp
        css = "app-inventory-panel{" + r + "}";
        // the numbers are tiny bitmaps drawn by the game; stretched smoothly they turn blurry, so when scaling up they are stretched in whole pixels instead
        if (sc > 1)
          css +=
            "app-inventory-slot canvas{image-rendering:pixelated !important}";
      }
    }
    if (HUD.classic && CHUD.alive)
      css += "app-inventory-panel{opacity:0 !important}"; // invisible but still there: the classic bar reads from it
    if (css === hudCss) return;
    hudCss = css;
    hudStyle.textContent = css;
    if (css && !hudStyle.isConnected)
      (document.head || document.documentElement).append(hudStyle);
  };
  setInterval(() => {
    try {
      hudApply();
    } catch (e) {}
  }, 250); // the bar is built by the game and its width can change, so it is re-measured now and then




  window.__postfx = {
    bloom: BLOOM,
    cam: CAM,
    dof: DOF,
    grade: GRADE,
    sharp: SHARP,
    fog: FOG,
    sun: SUN,
    light: LIGHT,
    precip: PRECIP,
    wet: WET,
    snowg: SNOWG,
    hud: HUD,
    sky: SKY,
    skyStats: () => skyMod && skyMod.S,
    stats: () => cam && cam.st,
    classicSupplies: CHUD,
    detach,
  };
  try {
    installUI({
      bloom: BLOOM,
      cam: CAM,
      dof: DOF,
      grade: GRADE,
      sharp: SHARP,
      fog: FOG,
      sun: SUN,
      light: LIGHT,
      precip: PRECIP,
      wet: WET,
      snowg: SNOWG,
      hud: HUD,
      sky: SKY,
    });
    reportLog("info", "ProTanki Pack UI & hooks initialized.");
  } catch (e) {
    reportLog("error", "installUI failed: " + ((e && e.stack) || e));
  }
})();
// Hiệu ứng sân khấu vẽ bằng canvas (không dùng thư viện ngoài).
//   StageFx.setAmbient('fireworks' | 'confetti' | 'snow' | 'sparkles' | 'none')  – chạy nền, cường độ nhẹ
//   StageFx.spin(true | false)                                                   – pháo hoa dày hơn trong lúc quay số
//   StageFx.burst('confetti' | 'fireworks' | 'none')                             – bùng nổ khi chúc mừng
//
// Pháo hoa: chuyển thể từ "code-phao-hoa-html" (Firework Simulator – Caleb Miller, giấy phép MIT).
//   - Quả pháo bay lên có đuôi tia lửa lắc nhẹ, nổ ở độ cao ngẫu nhiên, đủ kiểu: cúc, vòng, cọ, liễu, chéo,
//     hoa, lá rơi, nổ lách tách, đuôi ngựa, nhấp nháy, "ma" (hiện màu muộn); có nhụy và tia phụ.
//   - Vật lý: trọng lực + lực cản không khí, tốc độ bù theo khung hình (chạy đều ở 30–144 Hz).
//   - Vẽ 2 lớp: lớp vệt sáng (mờ dần) + lớp lõi trắng (xoá mỗi khung). Màn chiếu có ảnh nền nên lớp vệt
//     mờ dần về TRONG SUỐT (destination-out) thay vì phủ đen như bản gốc.
(function () {
    'use strict';
    var reduced = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    var rnd = function (a, b) { return a + Math.random() * (b - a); };
    var pick = function (arr) { return arr[(Math.random() * arr.length) | 0]; };
    var PI_2 = Math.PI * 2, PI_HALF = Math.PI * 0.5;

    // ================================================================== Pháo hoa
    var COLOR = { Red: '#ff0043', Green: '#14fc56', Blue: '#1e7fff', Purple: '#e60aff', Gold: '#ffbf36', White: '#ffffff' };
    var INVISIBLE = '_INVISIBLE_';
    var COLOR_CODES = Object.keys(COLOR).map(function (k) { return COLOR[k]; });
    var COLOR_CODES_W_INVIS = COLOR_CODES.concat([INVISIBLE]);
    var GRAVITY = 0.9;
    var hw = navigator.hardwareConcurrency || 4;
    var quality = hw >= 8 ? 3 : 2;                // 2 = thường, 3 = cao (máy mạnh)
    var isHighQuality = quality === 3;

    var lastColor;
    function randomColorSimple() { return COLOR_CODES[(Math.random() * COLOR_CODES.length) | 0]; }
    function randomColor(o) {
        var c = randomColorSimple();
        if (o && o.limitWhite && c === COLOR.White && Math.random() < 0.6) c = randomColorSimple();
        // Nền sân khấu màu đỏ: giảm bớt pháo đỏ cho nổi bật
        if (c === COLOR.Red && Math.random() < 0.5) c = randomColorSimple();
        if (o && o.notSame) while (c === lastColor) c = randomColorSimple();
        else if (o && o.notColor) while (c === o.notColor) c = randomColorSimple();
        lastColor = c;
        return c;
    }
    function whiteOrGold() { return Math.random() < 0.5 ? COLOR.Gold : COLOR.White; }
    function makePistilColor(c) { return (c === COLOR.White || c === COLOR.Gold) ? randomColor({ notColor: c }) : whiteOrGold(); }

    // ---------------------------------------------------------------- Các kiểu pháo
    var shellTypes = {
        'Crysanthemum': function (size) {
            var glitter = Math.random() < 0.25, single = Math.random() < 0.72;
            var color = single ? randomColor({ limitWhite: true }) : [randomColor(), randomColor({ notSame: true })];
            var pistil = single && Math.random() < 0.42, pistilColor = pistil && makePistilColor(color);
            var secondColor = single && (Math.random() < 0.2 || color === COLOR.White) ? pistilColor || randomColor({ notColor: color, limitWhite: true }) : null;
            var streamers = !pistil && color !== COLOR.White && Math.random() < 0.42;
            return { shellSize: size, spreadSize: 300 + size * 100, starLife: 900 + size * 200, starDensity: glitter ? 1.1 : (isHighQuality ? 1.2 : 1.25),
                color: color, secondColor: secondColor, glitter: glitter ? 'light' : '', glitterColor: whiteOrGold(), pistil: pistil, pistilColor: pistilColor, streamers: streamers };
        },
        'Ghost': function (size) {
            var s = shellTypes.Crysanthemum(size), ghost = randomColor({ notColor: COLOR.White });
            s.starLife *= 1.5; s.streamers = true;
            s.pistil = Math.random() < 0.42; s.pistilColor = s.pistil && makePistilColor(ghost);
            s.color = INVISIBLE; s.secondColor = ghost; s.glitter = '';
            return s;
        },
        'Strobe': function (size) {
            var color = randomColor({ limitWhite: true });
            return { shellSize: size, spreadSize: 280 + size * 92, starLife: 1100 + size * 200, starLifeVariation: 0.4, starDensity: 1.1, color: color,
                glitter: 'light', glitterColor: COLOR.White, strobe: true, strobeColor: Math.random() < 0.5 ? COLOR.White : null,
                pistil: Math.random() < 0.5, pistilColor: makePistilColor(color) };
        },
        'Palm': function (size) {
            var thick = Math.random() < 0.5;
            return { shellSize: size, color: randomColor(), spreadSize: 250 + size * 75, starDensity: thick ? 0.15 : 0.4, starLife: 1800 + size * 200, glitter: thick ? 'thick' : 'heavy' };
        },
        'Ring': function (size) {
            var color = randomColor(), pistil = Math.random() < 0.75;
            return { shellSize: size, ring: true, color: color, spreadSize: 300 + size * 100, starLife: 900 + size * 200, starCount: 2.2 * PI_2 * (size + 1),
                pistil: pistil, pistilColor: makePistilColor(color), glitter: !pistil ? 'light' : '', glitterColor: color === COLOR.Gold ? COLOR.Gold : COLOR.White,
                streamers: Math.random() < 0.3 };
        },
        'Crossette': function (size) {
            var color = randomColor({ limitWhite: true });
            return { shellSize: size, spreadSize: 300 + size * 100, starLife: 750 + size * 160, starLifeVariation: 0.4, starDensity: 0.85, color: color,
                crossette: true, pistil: Math.random() < 0.5, pistilColor: makePistilColor(color) };
        },
        'Floral': function (size) {
            return { shellSize: size, spreadSize: 300 + size * 120, starDensity: 0.12, starLife: 500 + size * 50, starLifeVariation: 0.5,
                color: Math.random() < 0.65 ? 'random' : (Math.random() < 0.15 ? randomColor() : [randomColor(), randomColor({ notSame: true })]), floral: true };
        },
        'Falling Leaves': function (size) {
            return { shellSize: size, color: INVISIBLE, spreadSize: 300 + size * 120, starDensity: 0.12, starLife: 500 + size * 50, starLifeVariation: 0.5,
                glitter: 'medium', glitterColor: COLOR.Gold, fallingLeaves: true };
        },
        'Willow': function (size) {
            return { shellSize: size, spreadSize: 300 + size * 100, starDensity: 0.6, starLife: 3000 + size * 300, glitter: 'willow', glitterColor: COLOR.Gold, color: INVISIBLE };
        },
        'Crackle': function (size) {
            var color = Math.random() < 0.75 ? COLOR.Gold : randomColor();
            return { shellSize: size, spreadSize: 380 + size * 75, starDensity: 1, starLife: 600 + size * 100, starLifeVariation: 0.32, glitter: 'light',
                glitterColor: COLOR.Gold, color: color, crackle: true, pistil: Math.random() < 0.65, pistilColor: makePistilColor(color) };
        },
        'Horse Tail': function (size) {
            var color = randomColor();
            return { shellSize: size, horsetail: true, color: color, spreadSize: 250 + size * 38, starDensity: 0.9, starLife: 2500 + size * 300,
                glitter: 'medium', glitterColor: Math.random() < 0.5 ? whiteOrGold() : color, strobe: color === COLOR.White };
        }
    };
    var shellNames = Object.keys(shellTypes);
    var heavyShells = ['Falling Leaves', 'Floral', 'Willow'];          // nặng máy, hạn chế khi bắn dày
    function randomShellName(fast) {
        var name;
        do { name = Math.random() < 0.5 ? 'Crysanthemum' : pick(shellNames); } while (fast && heavyShells.indexOf(name) >= 0);
        return name;
    }

    // ---------------------------------------------------------------- Bộ máy pháo hoa (mỗi lớp canvas 1 bộ)
    function collection() { var c = {}; COLOR_CODES_W_INVIS.forEach(function (k) { c[k] = []; }); return c; }

    function Fireworks(zIndex) {
        this.trails = document.createElement('canvas');
        this.main = document.createElement('canvas');
        [this.trails, this.main].forEach(function (c) {
            c.style.cssText = 'position:fixed;inset:0;width:100vw;height:100vh;pointer-events:none;z-index:' + zIndex;
            c.setAttribute('aria-hidden', 'true');
            document.body.appendChild(c);
        });
        this.tctx = this.trails.getContext('2d');
        this.mctx = this.main.getContext('2d');
        this.stars = collection();
        this.sparks = collection();
        this.starPool = [];
        this.sparkPool = [];
        this.flashes = [];
        this.frame = 0;
        this.idleFrames = 0;
        this.resize();
        window.addEventListener('resize', this.resize.bind(this));
    }
    Fireworks.prototype.resize = function () {
        this.dpr = Math.min(window.devicePixelRatio || 1, 2);
        this.w = window.innerWidth;
        this.h = window.innerHeight;
        // Kích thước pháo chuẩn theo màn 1920 px; màn nhỏ thì thu lại cho cân
        this.scale = Math.max(0.55, Math.min(1.15, this.w / 1920));
        var self = this;
        [this.trails, this.main].forEach(function (c) { c.width = self.w * self.dpr; c.height = self.h * self.dpr; });
    };
    Fireworks.prototype.addStar = function (x, y, color, angle, speed, life, offX, offY) {
        var s = this.starPool.pop() || {};
        s.visible = true; s.heavy = false; s.x = s.prevX = x; s.y = s.prevY = y; s.color = color;
        s.speedX = Math.sin(angle) * speed + (offX || 0);
        s.speedY = Math.cos(angle) * speed + (offY || 0);
        s.life = s.fullLife = life;
        s.spinAngle = Math.random() * PI_2; s.spinSpeed = 0.8; s.spinRadius = 0;
        s.sparkFreq = 0; s.sparkSpeed = 1; s.sparkTimer = 0; s.sparkColor = color; s.sparkLife = 750; s.sparkLifeVariation = 0.25;
        s.strobe = false; s.onDeath = null; s.secondColor = null; s.transitionTime = 0; s.colorChanged = false; s.updateFrame = -1;
        this.stars[color].push(s);
        return s;
    };
    Fireworks.prototype.addSpark = function (x, y, color, angle, speed, life) {
        var s = this.sparkPool.pop() || {};
        s.x = s.prevX = x; s.y = s.prevY = y; s.color = color;
        s.speedX = Math.sin(angle) * speed; s.speedY = Math.cos(angle) * speed; s.life = life;
        this.sparks[color].push(s);
        return s;
    };
    Fireworks.prototype.count = function () {
        var n = this.flashes.length, self = this;
        COLOR_CODES_W_INVIS.forEach(function (c) { n += self.stars[c].length + self.sparks[c].length; });
        return n;
    };

    function createParticleArc(start, arcLength, count, randomness, factory) {
        var delta = arcLength / count, end = start + arcLength - delta * 0.5, a;
        if (end > start) for (a = start; a < end; a += delta) factory(a + Math.random() * delta * randomness);
        else for (a = start; a > end; a += delta) factory(a + Math.random() * delta * randomness);
    }
    function createBurst(count, factory, startAngle, arcLength) {
        startAngle = startAngle || 0;
        arcLength = arcLength || PI_2;
        var R = 0.5 * Math.sqrt(count / Math.PI), C = 2 * R * Math.PI, C_HALF = C / 2;
        for (var i = 0; i <= C_HALF; i++) {
            var ringSize = Math.cos(i / C_HALF * PI_HALF), perRing = C * ringSize, perArc = perRing * (arcLength / PI_2);
            var inc = PI_2 / perRing, offset = Math.random() * inc + startAngle, maxRand = inc * 0.33;
            for (var j = 0; j < perArc; j++) factory(inc * j + offset + Math.random() * maxRand, ringSize);
        }
    }

    // Hiệu ứng khi một ngôi sao tắt
    function crossetteEffect(fw, star) {
        createParticleArc(Math.random() * PI_HALF, PI_2, 4, 0.5, function (a) {
            fw.addStar(star.x, star.y, star.color, a, Math.random() * 0.6 + 0.75, 600);
        });
    }
    function floralEffect(fw, star) {
        createBurst(12 + 6 * quality, function (a, m) {
            fw.addStar(star.x, star.y, star.color, a, m * 2.4, 1000 + Math.random() * 300, star.speedX, star.speedY);
        });
        fw.flashes.push({ x: star.x, y: star.y, r: 46 });
    }
    function fallingLeavesEffect(fw, star) {
        createBurst(7, function (a, m) {
            var s = fw.addStar(star.x, star.y, INVISIBLE, a, m * 2.4, 2400 + Math.random() * 600, star.speedX, star.speedY);
            s.sparkColor = COLOR.Gold; s.sparkFreq = 144 / quality; s.sparkSpeed = 0.28; s.sparkLife = 750; s.sparkLifeVariation = 3.2;
        });
        fw.flashes.push({ x: star.x, y: star.y, r: 46 });
    }
    function crackleEffect(fw, star) {
        createParticleArc(0, PI_2, isHighQuality ? 32 : 16, 1.8, function (a) {
            fw.addSpark(star.x, star.y, COLOR.Gold, a, Math.pow(Math.random(), 0.45) * 2.4, 300 + Math.random() * 200);
        });
    }

    var GLITTER = {
        light: [400, 0.3, 300, 2], medium: [200, 0.44, 700, 2], heavy: [80, 0.8, 1400, 2],
        thick: [16, isHighQuality ? 1.65 : 1.5, 1400, 3], streamer: [32, 1.05, 620, 2], willow: [120, 0.34, 1400, 3.8]
    };

    function Shell(fw, o) {
        for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) this[k] = o[k];
        this.fw = fw;
        this.starLifeVariation = o.starLifeVariation || 0.125;
        this.color = o.color || randomColor();
        this.glitterColor = o.glitterColor || this.color;
        // Kích thước theo màn hình
        this.spreadSize *= fw.scale;
        if (!this.starCount) {
            var scaled = this.spreadSize / 54;
            this.starCount = Math.max(6, scaled * scaled * (o.starDensity || 1));
        }
    }
    /** position: 0..1 theo chiều ngang; launchHeight: 0..1 (1 = nổ cao nhất). */
    Shell.prototype.launch = function (position, launchHeight) {
        var fw = this.fw, w = fw.w, h = fw.h, hpad = 60, vpad = 50, minHeight = h - h * 0.45;
        var launchX = position * (w - hpad * 2) + hpad, launchY = h;
        var burstY = minHeight - launchHeight * (minHeight - vpad);
        var v = Math.pow((launchY - burstY) * 0.04, 0.64);
        var self = this;
        var comet = this.comet = fw.addStar(launchX, launchY, typeof this.color === 'string' && this.color !== 'random' ? this.color : COLOR.White,
            Math.PI, v * (this.horsetail ? 1.2 : 1), v * (this.horsetail ? 100 : 400));
        comet.heavy = true;
        comet.spinRadius = rnd(0.32, 0.85);
        comet.sparkFreq = isHighQuality ? 8 : 32 / quality;
        comet.sparkLife = 320;
        comet.sparkLifeVariation = 3;
        if (this.glitter === 'willow' || this.fallingLeaves) { comet.sparkFreq = 20 / quality; comet.sparkSpeed = 0.5; comet.sparkLife = 500; }
        if (this.color === INVISIBLE) comet.sparkColor = COLOR.Gold;
        if (Math.random() > 0.4 && !this.horsetail) { comet.secondColor = INVISIBLE; comet.transitionTime = Math.pow(Math.random(), 1.5) * 700 + 500; }
        comet.onDeath = function (c) { self.burst(c.x, c.y); };
    };
    Shell.prototype.burst = function (x, y) {
        var fw = this.fw, self = this, speed = this.spreadSize / 96;
        var color, onDeath, g = GLITTER[this.glitter], sparkFreq, sparkSpeed, sparkLife, sparkLifeVariation = 0.25;
        if (this.crossette) onDeath = function (s) { crossetteEffect(fw, s); };
        if (this.crackle) onDeath = function (s) { crackleEffect(fw, s); };
        if (this.floral) onDeath = function (s) { floralEffect(fw, s); };
        if (this.fallingLeaves) onDeath = function (s) { fallingLeavesEffect(fw, s); };
        if (g) { sparkFreq = g[0] / quality; sparkSpeed = g[1]; sparkLife = g[2]; sparkLifeVariation = g[3]; }

        function glitterize(star) {
            if (!self.glitter) return;
            star.sparkFreq = sparkFreq; star.sparkSpeed = sparkSpeed; star.sparkLife = sparkLife;
            star.sparkLifeVariation = sparkLifeVariation; star.sparkColor = self.glitterColor; star.sparkTimer = Math.random() * star.sparkFreq;
        }
        function starFactory(angle, speedMult) {
            var initial = self.spreadSize / 1800;
            var star = fw.addStar(x, y, color || randomColor(), angle, speedMult * speed,
                self.starLife + Math.random() * self.starLife * self.starLifeVariation,
                self.horsetail ? self.comet && self.comet.speedX : 0,
                self.horsetail ? self.comet && self.comet.speedY : -initial);
            if (self.secondColor) { star.transitionTime = self.starLife * (Math.random() * 0.05 + 0.32); star.secondColor = self.secondColor; }
            if (self.strobe) {
                star.transitionTime = self.starLife * (Math.random() * 0.08 + 0.46);
                star.strobe = true;
                star.strobeFreq = Math.random() * 20 + 40;
                if (self.strobeColor) star.secondColor = self.strobeColor;
            }
            star.onDeath = onDeath;
            glitterize(star);
        }

        if (typeof this.color === 'string') {
            color = this.color === 'random' ? null : this.color;
            if (this.ring) {
                var ringStart = Math.random() * Math.PI, squash = Math.pow(Math.random(), 2) * 0.85 + 0.15;
                createParticleArc(0, PI_2, this.starCount, 0, function (angle) {
                    var ix = Math.sin(angle) * speed * squash, iy = Math.cos(angle) * speed;
                    var star = fw.addStar(x, y, color, Math.atan2(ix, iy) + ringStart, Math.sqrt(ix * ix + iy * iy),
                        self.starLife + Math.random() * self.starLife * self.starLifeVariation);
                    glitterize(star);
                });
            } else {
                createBurst(this.starCount, starFactory);
            }
        } else if (Array.isArray(this.color)) {
            if (Math.random() < 0.5) {
                var start = Math.random() * Math.PI;
                color = this.color[0]; createBurst(this.starCount, starFactory, start, Math.PI);
                color = this.color[1]; createBurst(this.starCount, starFactory, start + Math.PI, Math.PI);
            } else {
                color = this.color[0]; createBurst(this.starCount / 2, starFactory);
                color = this.color[1]; createBurst(this.starCount / 2, starFactory);
            }
        }
        // Spread đã nhân scale; shell con dùng spread gốc nên chia lại cho đúng tỉ lệ
        var base = this.spreadSize / fw.scale;
        if (this.pistil) {
            new Shell(fw, { spreadSize: base * 0.5, starLife: this.starLife * 0.6, starLifeVariation: this.starLifeVariation, starDensity: 1.4,
                color: this.pistilColor, glitter: 'light', glitterColor: this.pistilColor === COLOR.Gold ? COLOR.Gold : COLOR.White }).burst(x, y);
        }
        if (this.streamers) {
            new Shell(fw, { spreadSize: base * 0.9, starLife: this.starLife * 0.8, starLifeVariation: this.starLifeVariation,
                starCount: Math.floor(Math.max(6, this.spreadSize / 45)), color: COLOR.White, glitter: 'streamer' }).burst(x, y);
        }
        fw.flashes.push({ x: x, y: y, r: this.spreadSize / 4 });
    };

    Fireworks.prototype.launchRandom = function (size, fast, x, height) {
        var name = randomShellName(fast);
        var shell = new Shell(this, shellTypes[name](size));
        shell.launch(x == null ? rnd(0.06, 0.94) : x, height == null ? rnd(0.35, 1) : height);
        return shell;
    };

    Fireworks.prototype.step = function (frameTime, lag) {
        var self = this, speed = lag, timeStep = frameTime;
        this.frame++;
        var starDrag = 1 - (1 - 0.98) * speed, heavyDrag = 1 - (1 - 0.992) * speed, sparkDrag = 1 - (1 - 0.9) * speed;
        var gAcc = timeStep / 1000 * GRAVITY;

        COLOR_CODES_W_INVIS.forEach(function (color) {
            var stars = self.stars[color], i, s;
            for (i = stars.length - 1; i >= 0; i--) {
                s = stars[i];
                if (s.updateFrame === self.frame) continue;
                s.updateFrame = self.frame;
                s.life -= timeStep;
                if (s.life <= 0) {
                    stars.splice(i, 1);
                    // Gọi hiệu ứng tắt TRƯỚC khi trả về bể (hiệu ứng còn đọc x, y, màu của ngôi sao này)
                    if (s.onDeath) s.onDeath(s);
                    s.onDeath = null;
                    self.starPool.push(s);
                    continue;
                }
                var burn = Math.pow(s.life / s.fullLife, 0.5), burnInv = 1 - burn;
                s.prevX = s.x; s.prevY = s.y;
                s.x += s.speedX * speed; s.y += s.speedY * speed;
                var d = s.heavy ? heavyDrag : starDrag;
                s.speedX *= d; s.speedY *= d;
                s.speedY += gAcc;
                if (s.spinRadius) {
                    s.spinAngle += s.spinSpeed * speed;
                    s.x += Math.sin(s.spinAngle) * s.spinRadius * speed;
                    s.y += Math.cos(s.spinAngle) * s.spinRadius * speed;
                }
                if (s.sparkFreq) {
                    s.sparkTimer -= timeStep;
                    while (s.sparkTimer < 0) {
                        s.sparkTimer += s.sparkFreq * 0.75 + s.sparkFreq * burnInv * 4;
                        self.addSpark(s.x, s.y, s.sparkColor, Math.random() * PI_2, Math.random() * s.sparkSpeed * burn,
                            s.sparkLife * 0.8 + Math.random() * s.sparkLifeVariation * s.sparkLife);
                    }
                }
                if (s.life < s.transitionTime) {
                    if (s.secondColor && !s.colorChanged) {
                        s.colorChanged = true;
                        s.color = s.secondColor;
                        stars.splice(i, 1);
                        self.stars[s.secondColor].push(s);
                        if (s.secondColor === INVISIBLE) s.sparkFreq = 0;
                    }
                    if (s.strobe) s.visible = Math.floor(s.life / s.strobeFreq) % 3 === 0;
                }
            }
            var sparks = self.sparks[color];
            for (i = sparks.length - 1; i >= 0; i--) {
                s = sparks[i];
                s.life -= timeStep;
                if (s.life <= 0) { sparks.splice(i, 1); self.sparkPool.push(s); continue; }
                s.prevX = s.x; s.prevY = s.y;
                s.x += s.speedX * speed; s.y += s.speedY * speed;
                s.speedX *= sparkDrag; s.speedY *= sparkDrag;
                s.speedY += gAcc;
            }
        });
        this.render(speed);
    };

    Fireworks.prototype.render = function (speed) {
        var t = this.tctx, m = this.mctx, dpr = this.dpr, w = this.w, h = this.h, self = this;
        var alive = this.count();
        if (!alive) {
            // Không còn hạt nào: xoá sạch sau ~1 giây để không còn vệt mờ do làm tròn alpha
            if (++this.idleFrames === 60) { t.setTransform(1, 0, 0, 1, 0, 0); t.clearRect(0, 0, this.trails.width, this.trails.height); }
            if (this.idleFrames <= 2) { m.setTransform(1, 0, 0, 1, 0, 0); m.clearRect(0, 0, this.main.width, this.main.height); }
            if (this.idleFrames > 60) return;
        } else {
            this.idleFrames = 0;
        }
        t.setTransform(dpr, 0, 0, dpr, 0, 0);
        m.setTransform(dpr, 0, 0, dpr, 0, 0);

        // Lớp vệt: mờ dần về trong suốt
        t.globalCompositeOperation = 'destination-out';
        t.fillStyle = 'rgba(0,0,0,' + Math.min(1, 0.175 * speed) + ')';
        t.fillRect(0, 0, w, h);
        m.clearRect(0, 0, w, h);

        // Quầng sáng lúc nổ
        t.globalCompositeOperation = 'source-over';
        while (this.flashes.length) {
            var f = this.flashes.pop(), gr = t.createRadialGradient(f.x, f.y, 0, f.x, f.y, f.r);
            gr.addColorStop(0.024, 'rgba(255,255,255,1)');
            gr.addColorStop(0.125, 'rgba(255,160,20,0.2)');
            gr.addColorStop(0.32, 'rgba(255,140,20,0.11)');
            gr.addColorStop(1, 'rgba(255,120,20,0)');
            t.fillStyle = gr;
            t.fillRect(f.x - f.r, f.y - f.r, f.r * 2, f.r * 2);
        }

        t.globalCompositeOperation = 'lighten';
        t.lineWidth = 3;
        t.lineCap = 'round';
        m.strokeStyle = '#fff';
        m.lineWidth = 1;
        m.beginPath();
        COLOR_CODES.forEach(function (color) {
            t.strokeStyle = color;
            t.beginPath();
            self.stars[color].forEach(function (s) {
                if (!s.visible) return;
                t.moveTo(s.x, s.y); t.lineTo(s.prevX, s.prevY);
                m.moveTo(s.x, s.y); m.lineTo(s.x - s.speedX * 1.6, s.y - s.speedY * 1.6);
            });
            t.stroke();
        });
        m.stroke();

        t.lineWidth = isHighQuality ? 0.75 : 1;
        t.lineCap = 'butt';
        COLOR_CODES.forEach(function (color) {
            t.strokeStyle = color;
            t.beginPath();
            self.sparks[color].forEach(function (s) { t.moveTo(s.x, s.y); t.lineTo(s.prevX, s.prevY); });
            t.stroke();
        });
        t.globalCompositeOperation = 'source-over';
    };

    // ================================================================== Giấy màu / tuyết / lấp lánh (giữ như cũ)
    var COLORS = ['#ffd86b', '#ff5b45', '#ffffff', '#ff9ec7', '#7fe0ff', '#9dff8a', '#c8102e', '#f2b632'];

    function Layer(canvas, maxParticles) {
        this.c = canvas;
        this.ctx = canvas.getContext('2d');
        this.p = [];
        this.max = maxParticles;
        this.dpr = Math.min(window.devicePixelRatio || 1, 2);
        this.resize();
        window.addEventListener('resize', this.resize.bind(this));
    }
    Layer.prototype.resize = function () {
        this.w = window.innerWidth;
        this.h = window.innerHeight;
        this.c.width = this.w * this.dpr;
        this.c.height = this.h * this.dpr;
        this.ctx.setTransform(this.dpr, 0, 0, this.dpr, 0, 0);
    };
    Layer.prototype.add = function (q) { if (this.p.length < this.max) this.p.push(q); };
    Layer.prototype.step = function (dt) {
        var ctx = this.ctx, w = this.w, h = this.h, k = dt * 60;
        if (!this.p.length && this.empty) return;
        ctx.clearRect(0, 0, w, h);
        this.empty = !this.p.length;
        for (var i = this.p.length - 1; i >= 0; i--) {
            var q = this.p[i];
            q.life -= dt;
            if (q.life <= 0 || q.y > h + 40 || q.x < -60 || q.x > w + 60) { this.p.splice(i, 1); continue; }
            var drag = Math.pow(q.drag, k);
            q.vx *= drag; q.vy = q.vy * drag + q.g * dt;
            q.x += q.vx * dt; q.y += q.vy * dt;
            q.rot += q.vr * dt;
            var a = Math.min(1, q.life / q.fade);
            if (q.kind === 'twinkle') a *= 0.5 + 0.5 * Math.sin(q.life * q.tw);
            ctx.globalAlpha = Math.max(0, a);
            ctx.fillStyle = q.color;
            if (q.kind === 'rect') {
                ctx.save();
                ctx.translate(q.x, q.y);
                ctx.rotate(q.rot);
                ctx.scale(1, Math.cos(q.rot * 1.7));
                ctx.fillRect(-q.s / 2, -q.s / 3, q.s, q.s * 0.66);
                ctx.restore();
            } else if (q.kind === 'twinkle') {
                star(ctx, q.x, q.y, q.s);
            } else {
                ctx.beginPath();
                ctx.arc(q.x, q.y, q.s, 0, Math.PI * 2);
                ctx.fill();
            }
        }
        ctx.globalAlpha = 1;
    };
    function star(ctx, x, y, s) {
        ctx.beginPath();
        ctx.moveTo(x, y - s);
        ctx.quadraticCurveTo(x, y, x + s, y);
        ctx.quadraticCurveTo(x, y, x, y + s);
        ctx.quadraticCurveTo(x, y, x - s, y);
        ctx.quadraticCurveTo(x, y, x, y - s);
        ctx.fill();
    }
    function particle(o) {
        return Object.assign({ x: 0, y: 0, vx: 0, vy: 0, g: 0, drag: 1, life: 2, fade: 0.6, s: 3, rot: 0, vr: 0, color: '#fff', kind: 'dot' }, o);
    }
    function confettiPiece(layer, x, y, vx, vy) {
        layer.add(particle({ x: x, y: y, vx: vx, vy: vy, g: 380, drag: 0.985, life: rnd(3, 5), fade: 1, s: rnd(8, 14),
            rot: rnd(0, 6), vr: rnd(-8, 8), color: pick(COLORS), kind: 'rect' }));
    }

    // ================================================================== Điều phối
    var ambient, burstLayer, fwBack, fwFront;
    var ambientType = 'none', spinOn = false, timer = 0, autoBack = 0, last = 0, running = false;
    var simSpeed = 1;          // tốc độ mô phỏng pháo hoa (cấu hình "Tốc độ bay của pháo hoa")

    function spawnAmbient(dt) {
        var L = ambient, w = L.w, h = L.h;
        timer += dt;
        switch (ambientType) {
            case 'confetti':
                if (timer > 0.12) { timer = 0; confettiPiece(L, rnd(0, w), -20, rnd(-40, 40), rnd(30, 90)); L.p[L.p.length - 1].g = 40; L.p[L.p.length - 1].life = 12; }
                break;
            case 'snow':
                if (timer > 0.08) { timer = 0; L.add(particle({ x: rnd(-20, w + 20), y: -10, vx: rnd(-15, 15), vy: rnd(30, 70), life: 30, fade: 2, s: rnd(1.5, 4), color: 'rgba(255,255,255,.9)' })); }
                break;
            case 'sparkles':
                if (timer > 0.09) { timer = 0; L.add(particle({ x: rnd(0, w), y: rnd(0, h), life: rnd(1.5, 3), fade: 1.5, s: rnd(3, 8), color: pick(['#fff', '#ffe7a3', '#fff3c4']), kind: 'twinkle', tw: rnd(4, 9) })); }
                break;
        }
    }

    /** Lịch bắn pháo nền: lúc quay dày đặc, lúc chờ thưa và chậm. */
    function scheduleBack(ms) {
        autoBack -= ms;
        if (autoBack > 0) return;
        if (spinOn) {
            // Đang quay: 1–2 quả cỡ vừa mỗi 0,3–0,65 giây
            fwBack.launchRandom(rnd(1, 2.2), true);
            if (Math.random() < 0.45) setTimeout(function () { fwBack.launchRandom(rnd(1, 2), true); }, 120);
            autoBack = rnd(300, 650);
        } else {
            // Nền lúc chờ: một quả rồi nghỉ cho tới khi nó tàn
            var shell = fwBack.launchRandom(rnd(1, 2.4), false);
            autoBack = 1600 + Math.random() * 1400 + shell.starLife;
        }
    }

    function loop(t) {
        var frameTime = Math.min(68, Math.max(1, t - last || 16.7));
        last = t;
        var dt = frameTime / 1000, lag = frameTime / 16.6667;
        if (!document.hidden) {
            if (ambientType !== 'none' && ambientType !== 'fireworks') spawnAmbient(dt);
            if (spinOn || ambientType === 'fireworks') scheduleBack(frameTime * simSpeed);
        }
        ambient.step(dt);
        burstLayer.step(dt);
        // Giống simSpeed của bản gốc: nhân cả thời gian (tuổi hạt, trọng lực) lẫn quãng di chuyển mỗi khung
        fwBack.step(frameTime * simSpeed, lag * simSpeed);
        fwFront.step(frameTime * simSpeed, lag * simSpeed);
        var busy = ambient.p.length || burstLayer.p.length || fwBack.count() || fwFront.count()
            || fwBack.idleFrames <= 60 || fwFront.idleFrames <= 60 || (ambientType !== 'none') || spinOn;
        if (busy) requestAnimationFrame(loop);
        else running = false;
    }

    function ensureRunning() {
        if (running) return;
        running = true;
        last = performance.now();
        requestAnimationFrame(loop);
    }

    window.StageFx = {
        init: function (ambientCanvas, burstCanvas) {
            ambient = new Layer(ambientCanvas, reduced ? 60 : 420);
            burstLayer = new Layer(burstCanvas, reduced ? 120 : 900);
            // Pháo nền nằm cùng tầng nền (sau nội dung); pháo chúc mừng nằm trên màn chúc mừng
            var zBack = getComputedStyle(ambientCanvas).zIndex, zFront = getComputedStyle(burstCanvas).zIndex;
            fwBack = new Fireworks(zBack === 'auto' ? -1 : zBack);
            fwFront = new Fireworks(zFront === 'auto' ? 72 : zFront);
        },
        /** percent: 30–200 (100 = tốc độ thật). */
        setSpeed: function (percent) {
            var p = Number(percent);
            simSpeed = isFinite(p) && p > 0 ? Math.max(0.3, Math.min(2, p / 100)) : 1;
        },
        setAmbient: function (type) {
            ambientType = reduced ? 'none' : (type || 'none');
            ambient.p.length = 0;
            autoBack = 600;
            ensureRunning();
        },
        spin: function (on) {
            spinOn = !!on && !reduced;
            if (spinOn) autoBack = 0;
            else if (ambientType === 'fireworks') autoBack = Math.max(autoBack, 2500);
            ensureRunning();
        },
        burst: function (type) {
            if (!burstLayer || type === 'none') return;
            var L = burstLayer, w = L.w, h = L.h, n = reduced ? 40 : 160;
            if (type === 'fireworks') {
                // Màn bắn chúc mừng: loạt pháo to liên tiếp (kiểu "finale")
                var count = reduced ? 2 : 14;
                for (var k = 0; k < count; k++) {
                    (function (k) {
                        setTimeout(function () {
                            fwFront.launchRandom(rnd(2, 3.2), true, k === 0 ? 0.5 : rnd(0.08, 0.92), rnd(0.45, 1));
                            ensureRunning();
                        }, k * 170 / simSpeed);
                    })(k);
                }
            } else {
                for (var i = 0; i < n; i++) {
                    var left = i % 2 === 0;
                    confettiPiece(L, left ? -10 : w + 10, h * rnd(0.55, 0.85), (left ? 1 : -1) * rnd(300, 900), -rnd(500, 1050));
                }
            }
            ensureRunning();
        }
    };
})();

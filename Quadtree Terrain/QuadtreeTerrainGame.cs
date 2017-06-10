using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

namespace Quadtree_Terrain
{
    /// <summary>
    /// This is the main type for your game.
    /// </summary>
    public class QuadtreeTerrainGame : Game
    {
        /// <summary>
        /// This class represents the root and branches of the terrain's quadtree.
        /// </summary>
        public class QuadtreeNode
        {
            public BoundingBox boundingBox;
            public QuadtreeNode topLeft, topRight, bottomLeft, bottomRight;

            public QuadtreeNode(BoundingBox boundingBox)
            {
                this.boundingBox = boundingBox;
            }
        }

        /// <summary>
        /// This class represents the leaf nodes of the terrain's quadtree that have actual vertex data.
        /// </summary>
        public class QuadtreeLeaf : QuadtreeNode
        {
            public VertexBuffer vertexBuffer;
            public IndexBuffer indexBuffer;

            public QuadtreeLeaf(BoundingBox boundingBox, Color[] heightMapColors, GraphicsDevice graphicsDevice) : base(boundingBox)
            {
                float tileSize = 2;
                float maxHeight = 100.0f;
                int heightMapWidth = 2048;

                int sx = (int)(boundingBox.Min.X / tileSize);
                int sy = (int)(boundingBox.Min.Z / tileSize);
                int width = (int)((boundingBox.Max.X - boundingBox.Min.X) / tileSize) + (sx < 2016 ? 1 : 0);
                int height = (int)((boundingBox.Max.Z - boundingBox.Min.Z) / tileSize) + (sy < 2016 ? 1 : 0);

                // vertex buffer generation
                VertexPosition[] vertices = new VertexPosition[width * height];

                this.boundingBox.Min.Y = maxHeight;
                this.boundingBox.Max.Y = 0;
                for (int y = 0; y < height; ++y)
                {
                    for (int x = 0; x < width; ++x)
                    {
                        float depth = heightMapColors[(sy + y) * heightMapWidth + sx + x].R / 255.0f;
                        vertices[y * width + x] = new VertexPosition(new Vector3(sx + x, depth, sy + y));
                        depth *= maxHeight;
                        this.boundingBox.Min.Y = MathHelper.Min(this.boundingBox.Min.Y, depth);
                        this.boundingBox.Max.Y = MathHelper.Max(this.boundingBox.Max.Y, depth);
                    }
                }

                vertexBuffer = new VertexBuffer(graphicsDevice, typeof(VertexPosition), vertices.Length, BufferUsage.WriteOnly);
                vertexBuffer.SetData<VertexPosition>(vertices);

                // index buffer generation
                int indicesPerRow = width * 2 + 2;
                int[] indices = new int[indicesPerRow * (height - 1)];

                for (int i = 0; i < height - 1; ++i)
                {
                    int index;
                    for (int ii = 0; ii < width; ++ii)
                    {
                        index = i * indicesPerRow + ii * 2;
                        indices[index] = ((i) * width + ii);
                        indices[index + 1] = ((i + 1) * width + ii);
                        //Debug.WriteLine(((i + ii % 2) * width + ii) + " " + ((i + 1 - ii % 2) * width + ii));
                    }
                    index = i * indicesPerRow + width * 2;
                    indices[index] = indices[index - 1];
                    indices[index + 1] = ((i + 1) * width);
                }

                indexBuffer = new IndexBuffer(graphicsDevice, typeof(int), indices.Length, BufferUsage.WriteOnly);
                indexBuffer.SetData(indices);
            }
        }

        GraphicsDeviceManager graphics;

        SpriteBatch spriteBatch;
        SpriteFont hudfont;

        Matrix view = Matrix.CreateLookAt(new Vector3(-500, 500, -500), new Vector3(-499, 500, -499), Vector3.Up);
        Matrix projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(60), 1980f / 1080f, 2f, 8000f);

        bool wireframe = false;
        bool antiAliasing = false;

        readonly Vector3 lightDirection = new Vector3(0.5f, -0.5f, 0.5f);
        const float ambientIntensity = 0.3f;

        Texture2D water;
        Texture2D waterNormalMap;
        VertexBuffer waterVertexBuffer;
        Effect waterEffect;
        float waterOffset;

        Texture2D heightMap;
        Texture2D grass;
        Texture2D rock;
        Texture2D sand;
        QuadtreeNode quadtreeRoot;
        Effect terrainEffect;
        float tileSize = 2;

        float cameraSpeed = 5.0f;
        const float MouseSpeed = 0.0001f;
        KeyboardState oldKeyboardState;
        MouseState oldMouseState;
        Point mouseStart;

        public QuadtreeTerrainGame()
        {
            graphics = new GraphicsDeviceManager(this);
            graphics.GraphicsProfile = GraphicsProfile.HiDef;
            graphics.PreferredBackBufferWidth = 1980;
            graphics.PreferredBackBufferHeight = 1080;
            Content.RootDirectory = "Content";
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            this.IsMouseVisible = true;

            base.Initialize();
        }

        /// <summary>
        /// Recursively subdivides the terrain quadtree.
        /// </summary>
        /// <param name="depth">The current zero-based depth of the algorithm.</param>
        /// <param name="heightMapColors">The array of heightmap colors.</param>
        /// <param name="node">The node of the quadtree that will be subdivided.</param>
        protected void BuildQuadtree(Color[] heightMapColors, QuadtreeNode node, int depth = 0)
        {
            Vector3 min = node.boundingBox.Min;
            Vector3 max = node.boundingBox.Max;
            Vector3 middle = (min + max) / 2;
            BoundingBox topLeft = new BoundingBox(min, middle);
            BoundingBox topRight = new BoundingBox(new Vector3(middle.X, 0, min.Z), new Vector3(max.X, 0, middle.Z));
            BoundingBox bottomLeft = new BoundingBox(new Vector3(min.X, 0, middle.Z), new Vector3(middle.X, 0, max.Z));
            BoundingBox bottomRight = new BoundingBox(middle, max);

            if (depth < 5)
            {
                BuildQuadtree(heightMapColors, node.topLeft = new QuadtreeNode(topLeft), depth + 1);
                BuildQuadtree(heightMapColors, node.topRight = new QuadtreeNode(topRight), depth + 1);
                BuildQuadtree(heightMapColors, node.bottomLeft = new QuadtreeNode(bottomLeft), depth + 1);
                BuildQuadtree(heightMapColors, node.bottomRight = new QuadtreeNode(bottomRight), depth + 1);
            }
            else
            {
                node.topLeft = new QuadtreeLeaf(topLeft, heightMapColors, graphics.GraphicsDevice);
                node.topRight = new QuadtreeLeaf(topRight, heightMapColors, graphics.GraphicsDevice);
                node.bottomLeft = new QuadtreeLeaf(bottomLeft, heightMapColors, graphics.GraphicsDevice);
                node.bottomRight = new QuadtreeLeaf(bottomRight, heightMapColors, graphics.GraphicsDevice);
            }

            float[] mins = new float[4] { node.topLeft.boundingBox.Min.Y,
                node.topRight.boundingBox.Min.Y,
                node.bottomLeft.boundingBox.Min.Y,
                node.bottomRight.boundingBox.Min.Y };
            Array.Sort(mins);
            float[] maxes = new float[4] { node.topLeft.boundingBox.Max.Y,
                node.topRight.boundingBox.Max.Y,
                node.bottomLeft.boundingBox.Max.Y,
                node.bottomRight.boundingBox.Max.Y };
            Array.Sort(maxes);

            node.boundingBox.Min.Y = MathHelper.Min(100.0f, mins[0]);
            node.boundingBox.Max.Y = MathHelper.Max(node.boundingBox.Max.Y, maxes[3]);
        }

        /// <summary>
        /// Builds a terrain quadtree from the heightmap.
        /// </summary>
        /// <param name="heightMap">The heightmap of the terrain.</param>
        protected QuadtreeNode BuildQuadtree(Texture2D heightMap)
        {
            QuadtreeNode root = new QuadtreeNode(new BoundingBox(Vector3.Zero, new Vector3(heightMap.Width * tileSize, 0.0f, heightMap.Height * tileSize)));

            Color[] heightMapColors = new Color[heightMap.Width * heightMap.Height];
            heightMap.GetData(heightMapColors);

            BuildQuadtree(heightMapColors, root);

            return root;
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);

            hudfont = Content.Load<SpriteFont>("hudFont");
            heightMap = Content.Load<Texture2D>("heightMap");

            grass = Content.Load<Texture2D>("grass");
            rock = Content.Load<Texture2D>("rock");
            sand = Content.Load<Texture2D>("sand");

            terrainEffect = Content.Load<Effect>("terrainEffect");
            terrainEffect.Parameters["MapWidth"].SetValue(heightMap.Width);
            terrainEffect.Parameters["MapHeight"].SetValue(heightMap.Height);
            terrainEffect.Parameters["TileSize"].SetValue(tileSize);
            terrainEffect.Parameters["MaxHeight"].SetValue(100.0f);
            terrainEffect.Parameters["WaterHeight"].SetValue(10.0f);
            terrainEffect.Parameters["HeightMap"].SetValue(heightMap);
            terrainEffect.Parameters["Texture0"].SetValue(grass);
            terrainEffect.Parameters["Texture1"].SetValue(rock);
            terrainEffect.Parameters["Texture2"].SetValue(sand);

            quadtreeRoot = BuildQuadtree(heightMap);

            water = Content.Load<Texture2D>("water");
            waterNormalMap = Content.Load<Texture2D>("waterNormalMap");

            waterEffect = Content.Load<Effect>("waterEffect");
            waterEffect.Parameters["ColorMap"].SetValue(water);
            waterEffect.Parameters["NormalMap"].SetValue(waterNormalMap);

            VertexPositionTexture[] waterVertices = new VertexPositionTexture[4];

            waterVertices[0] = new VertexPositionTexture(new Vector3(0, 10, 0), new Vector2(0, 0));
            waterVertices[1] = new VertexPositionTexture(new Vector3(0, 10, 16000), new Vector2(0, 16000 / 2));
            waterVertices[2] = new VertexPositionTexture(new Vector3(16000, 10, 0), new Vector2(16000 / 2, 0));
            waterVertices[3] = new VertexPositionTexture(new Vector3(16000, 10, 16000), new Vector2(16000 / 2, 16000 / 2));

            waterVertexBuffer = new VertexBuffer(
                GraphicsDevice, typeof(VertexPositionTexture), waterVertices.Length, BufferUsage.WriteOnly);
            waterVertexBuffer.SetData<VertexPositionTexture>(waterVertices);
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// game-specific content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }

        /// <summary>
        /// Uses the previous keyboard state to determine if a key was pressed.
        /// </summary>
        /// <param name="key">The key to compare.</param>
        private bool IsKeyPressed(Keys key)
        {
            return Keyboard.GetState().IsKeyDown(key) && oldKeyboardState.IsKeyUp(key);
        }

        /// <summary>
        /// Uses the previous keyboard state to determine if a key was released.
        /// </summary>
        /// <param name="key">The key to compare.</param>
        private bool IsKeyReleased(Keys key)
        {
            return Keyboard.GetState().IsKeyUp(key) && oldKeyboardState.IsKeyDown(key);
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
                Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            if (IsKeyReleased(Keys.F))
                graphics.ToggleFullScreen();
            if (IsKeyReleased(Keys.Q))
                wireframe = !wireframe;
            if (IsKeyReleased(Keys.P))
            {
                antiAliasing = !antiAliasing;
                graphics.PreferMultiSampling = antiAliasing;
                graphics.ApplyChanges();
                PresentationParameters presentationParameters = GraphicsDevice.PresentationParameters.Clone();
                presentationParameters.DepthStencilFormat = DepthFormat.Depth24;
                presentationParameters.MultiSampleCount = antiAliasing ? 8 : 0;
                GraphicsDevice.Reset(presentationParameters);
            }

            if (Keyboard.GetState().IsKeyDown(Keys.LeftShift))
            {
                cameraSpeed = 25.0f;
            }
            else
            {
                cameraSpeed = 5.0f;
            }

            if (Keyboard.GetState().IsKeyDown(Keys.W))
            {
                view.Translation += projection.Forward * cameraSpeed;
            }
            else if (Keyboard.GetState().IsKeyDown(Keys.S))
            {
                view.Translation += projection.Backward * cameraSpeed;
            }
            if (Keyboard.GetState().IsKeyDown(Keys.A))
            {
                view.Translation += projection.Right * cameraSpeed;
            }
            else if (Keyboard.GetState().IsKeyDown(Keys.D))
            {
                view.Translation += projection.Left * cameraSpeed;
            }

            if (Mouse.GetState().RightButton == ButtonState.Pressed)
            {
                if (oldMouseState.RightButton == ButtonState.Released)
                {
                    mouseStart = Mouse.GetState().Position;
                }
                view *= Matrix.CreateFromAxisAngle(view.Up, (Mouse.GetState().X - mouseStart.X) * MouseSpeed);
                view *= Matrix.CreateRotationX((Mouse.GetState().Y - mouseStart.Y) * MouseSpeed);
            }

            oldKeyboardState = Keyboard.GetState();
            oldMouseState = Mouse.GetState();
            base.Update(gameTime);
        }

        /// <summary>
        /// Recursively draws the quadtree terrain nodes that are in the viewing frustum.
        /// </summary>
        /// <param name="node">The node or leaf to draw.</param>
        protected void DrawQuadtree(QuadtreeNode node)
        {
            if (!(new BoundingFrustum(Matrix.Identity * view * projection)).Intersects(node.boundingBox)) return;

            if (node.GetType() == typeof(QuadtreeLeaf))
            {
                QuadtreeLeaf leaf = (QuadtreeLeaf)node;

                GraphicsDevice.SetVertexBuffer(leaf.vertexBuffer);
                GraphicsDevice.Indices = leaf.indexBuffer;

                GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleStrip, 0, 0, leaf.indexBuffer.IndexCount - 4);
            }
            else
            {
                DrawQuadtree(node.topLeft);
                DrawQuadtree(node.topRight);
                DrawQuadtree(node.bottomLeft);
                DrawQuadtree(node.bottomRight);
            }
        }

        /// <summary>
        /// Draws the terrain with with the custom effect.
        /// </summary>
        /// <param name="root">The root node of the quadtree.</param>
        protected void DrawTerrain(QuadtreeNode root)
        {
            terrainEffect.Parameters["WorldViewProjection"].SetValue(Matrix.Identity * view * projection);
            terrainEffect.Parameters["LightDirection"].SetValue(lightDirection);
            terrainEffect.Parameters["AmbientIntensity"].SetValue(ambientIntensity);

            foreach (EffectPass pass in terrainEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                DrawQuadtree(rot);
            }
        }

        /// <summary>
        /// Draws the water with the custom effect.
        /// </summary>
        protected void DrawWater()
        {
            GraphicsDevice.SetVertexBuffer(waterVertexBuffer);

            waterEffect.Parameters["WorldViewProjection"].SetValue(Matrix.Identity * view * projection);
            waterEffect.Parameters["LightDirection"].SetValue(lightDirection);
            waterEffect.Parameters["AmbientIntensity"].SetValue(ambientIntensity);
            waterEffect.Parameters["TextureOffset"].SetValue(waterOffset);
            waterOffset += 0.1f;
            if (waterOffset >= 8000) waterOffset = 0;

            foreach (EffectPass pass in waterEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);
            }
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            GraphicsDevice.DepthStencilState = DepthStencilState.Default;

            RasterizerState rasterizerState = new RasterizerState();
            rasterizerState.CullMode = CullMode.CullClockwiseFace;
            rasterizerState.FillMode = wireframe ? FillMode.WireFrame : FillMode.Solid;
            rasterizerState.MultiSampleAntiAlias = antiAliasing;
            graphics.GraphicsDevice.RasterizerState = rasterizerState;

            DrawTerrain(quadtreeRoot);
            DrawWater();

            spriteBatch.Begin();

            spriteBatch.DrawString(
                hudfont,
                "FPS              : " + (1 / (float)gameTime.ElapsedGameTime.TotalSeconds) + "\n" +
                "Wireframe     (Q): " + (wireframe ? "On" : "Off") + "\n" +
                "Anti-aliasing (P): " + (antiAliasing ? "On" : "Off") + "\n" +
                "Fullscreen    (F): " + (graphics.IsFullScreen ? "On" : "Off") + "\n" +
                "Camera           : " + view.Translation.ToString() + " " + view.Rotation.ToString(),
                new Vector2(10, 10),
                Color.Black);

            spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}

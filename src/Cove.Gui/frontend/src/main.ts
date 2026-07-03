const app = document.getElementById('app')!;

app.innerHTML = `
  <h1>Cove.Gui</h1>
  <p>Built with Ryn + Vite</p>
  <div class="card">
    <h2>Try IPC</h2>
    <div class="row">
      <input id="name" type="text" placeholder="Your name" value="World" />
      <button id="greet-btn">Greet</button>
    </div>
    <div class="result" id="result">Click Greet to call C#</div>
  </div>
`;

document.getElementById('greet-btn')!.addEventListener('click', async () => {
  const nameInput = document.getElementById('name') as HTMLInputElement;
  const result = await window.__ryn.invoke('app.greet', { name: nameInput.value });
  document.getElementById('result')!.textContent = result as string;
});
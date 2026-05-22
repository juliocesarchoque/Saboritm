// Usamos un bloque para evitar colisiones de constantes si el script se carga varias veces
if (typeof window.SUPABASE_CONFIG === 'undefined') {
    window.SUPABASE_CONFIG = {
        URL: 'https://urbpmczgltzjhuirkedu.supabase.co',
        KEY: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InVyYnBtY3pnbHR6amh1aXJrZWR1Iiwicm9sZSI6ImFub24iLCJpYXQiOjE3NzcyNDA1MDgsImV4cCI6MjA5MjgxNjUwOH0.OtNwBCu8tTMmbESq2UhDUeU0kg0pEyxnwa8atZlxTpk'
    };
}

/**
 * Función para obtener el cliente de Supabase de forma segura.
 * Verifica si ya existe en window.supabaseClient para no duplicar la conexión.
 */
function getSupabase() {
    if (window.supabaseClient) return window.supabaseClient;
    
    // El objeto 'supabase' (la librería) viene del CDN en el HTML
    if (window.supabase && typeof window.supabase.createClient === 'function') {
        window.supabaseClient = window.supabase.createClient(
            window.SUPABASE_CONFIG.URL, 
            window.SUPABASE_CONFIG.KEY
        );
        return window.supabaseClient;
    }
    
    console.error("La librería de Supabase no se ha detectado. Asegúrate de que el CDN esté antes que este script.");
    return null;
}

// Inicialización inmediata del alias para uso global
const supabaseClient = getSupabase();
const supabase = supabaseClient; // Alias para compatibilidad

// --- Configuración de la API .NET ---
let API_BASE_URL = 'https://saboritm-api.onrender.com/api';

const currentHost = window.location.hostname;
if (!currentHost || currentHost === 'localhost' || currentHost === '127.0.0.1') {
    API_BASE_URL = 'http://localhost:5048/api';
}

const API_HOST = API_BASE_URL.replace('/api', '');

/**
 * Variable para cachear la sesión y evitar llamadas redundantes a getSession()
 */
let cachedSession = null;
let lastSessionCheck = 0;

/**
 * Obtiene los headers de autorización si hay una sesión activa.
 */
async function getHeaders(extraHeaders = {}, includeContentType = true) {
    const client = getSupabase();
    let token = null;

    if (client) {
        // Cacheamos la sesión por 2 segundos para ráfagas de peticiones
        const now = Date.now();
        if (!cachedSession || (now - lastSessionCheck > 2000)) {
            const { data: { session } } = await client.auth.getSession();
            cachedSession = session;
            lastSessionCheck = now;
        }
        token = cachedSession?.access_token;
    } else {
        console.warn("⚠️ No se pudo inicializar el cliente de Supabase para obtener el token.");
    }
    
    if (!token) {
        console.warn("🔑 Petición enviada sin Token de autorización.");
    }

    return {
        ...(includeContentType ? { 'Content-Type': 'application/json' } : {}),
        ...(token ? { 'Authorization': `Bearer ${token}` } : {}),
        ...extraHeaders
    };
}

/**
 * Actualiza la visibilidad de elementos 'solo-admin'.
 */
async function actualizarInterfazAuth() {
    const client = getSupabase();
    if (!client) return;
    
    const { data: { session } } = await client.auth.getSession();
    const isAdmin = !!session;
    
    document.querySelectorAll('.solo-admin').forEach(el => {
        el.style.display = isAdmin ? 'block' : 'none';
        if (el.tagName === 'A' || el.tagName === 'BUTTON' || el.tagName === 'SPAN') {
             el.style.display = isAdmin ? 'inline-block' : 'none';
        }
    });

    const authBtn = document.getElementById('btn-auth-session');
    if (authBtn) {
        authBtn.innerHTML = isAdmin 
            ? '<i class="bi bi-box-arrow-right"></i> Salir' 
            : '<i class="bi bi-person-fill"></i> Admin';
        authBtn.onclick = isAdmin ? logout : () => window.location.href = 'login.html';
    }
}

async function logout() {
    const client = getSupabase();
    if (client) {
        await client.auth.signOut();
        window.location.reload();
    }
}

// --- Utilidades Varias ---
function resolveImgUrl(url) {
    if (!url || url.trim() === '') return null;
    if (/^https?:\/\//i.test(url)) return url;   
    if (url.startsWith('/')) return API_HOST + url; 
    return url; 
}

function normalizarTexto(texto) {
    if (!texto) return "";
    return texto.toString().toLowerCase().normalize("NFD").replace(/[\u0300-\u036f]/g, "").trim();
}

// --- Objeto API ---
const api = {
    get: async (endpoint) => {
        const response = await fetch(`${API_BASE_URL}${endpoint}`, { headers: await getHeaders() });
        return response.ok ? await response.json() : null;
    },
    post: async (endpoint, data) => {
        const response = await fetch(`${API_BASE_URL}${endpoint}`, {
            method: 'POST',
            headers: await getHeaders(),
            body: JSON.stringify(data)
        });
        return response.ok ? await response.json() : null;
    },
    put: async (endpoint, data) => {
        const response = await fetch(`${API_BASE_URL}${endpoint}`, {
            method: 'PUT',
            headers: await getHeaders(),
            body: JSON.stringify(data)
        });
        return response.ok ? (response.status !== 204 ? await response.json() : true) : null;
    },
    delete: async (endpoint) => {
        const response = await fetch(`${API_BASE_URL}${endpoint}`, {
            method: 'DELETE',
            headers: await getHeaders()
        });
        return response.ok;
    }
};

// Autoejecutar al cargar
if (getSupabase()) {
    actualizarInterfazAuth();
}

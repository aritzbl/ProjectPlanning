// Servicio para manejar popups de flujo BPM desde cualquier vista
class BmpFlowPopupService {
    constructor() {
        this.currentProjectId = null;
        this.initializeModals();
    }

    // Inicializar modales al cargar la página
    initializeModals() {
        // Solo inicializar si los modales no existen
        if (!document.getElementById('bmpFlowModalsContainer')) {
            this.injectModalHTML();
            this.attachEventListeners();
        }
    }

    // Inyectar HTML de modales en el DOM
    injectModalHTML() {
        const modalContainer = document.createElement('div');
        modalContainer.id = 'bmpFlowModalsContainer';
        modalContainer.innerHTML = `
            <!-- Modal para confirmar envío de avances -->
            <div class="modal fade" id="sendProgressModal" tabindex="-1" aria-labelledby="sendProgressModalLabel" aria-hidden="true">
                <div class="modal-dialog modal-dialog-centered">
                    <div class="modal-content">
                        <div class="modal-header bg-primary text-white">
                            <h5 class="modal-title" id="sendProgressModalLabel">
                                <i class="fas fa-paper-plane me-2"></i>
                                Envío de Avances
                            </h5>
                            <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body">
                            <div class="text-center mb-3">
                                <i class="fas fa-envelope fa-3x text-primary mb-3"></i>
                                <h5>¿Has enviado los avances del proyecto por email?</h5>
                                <p class="text-muted">Confirma que has enviado los avances del proyecto a los interesados correspondientes.</p>
                            </div>
                            <div class="alert alert-info" role="alert">
                                <i class="fas fa-info-circle me-2"></i>
                                <strong>Importante:</strong> Una vez confirmado, el proceso avanzará automáticamente a la siguiente etapa.
                            </div>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">
                                <i class="fas fa-times me-2"></i>Cancelar
                            </button>
                            <button type="button" class="btn btn-success" id="confirmSendProgress">
                                <i class="fas fa-check me-2"></i>Sí, envié los avances
                            </button>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Modal para notificar observaciones disponibles -->
            <div class="modal fade" id="observationsAvailableModal" tabindex="-1" aria-labelledby="observationsAvailableModalLabel" aria-hidden="true">
                <div class="modal-dialog modal-dialog-centered">
                    <div class="modal-content">
                        <div class="modal-header bg-warning text-dark">
                            <h5 class="modal-title" id="observationsAvailableModalLabel">
                                <i class="fas fa-eye me-2"></i>
                                Observaciones Disponibles
                            </h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body">
                            <div class="text-center mb-3">
                                <i class="fas fa-clipboard-list fa-3x text-warning mb-3"></i>
                                <h5>Tienes observaciones para revisar</h5>
                                <p class="text-muted">Se han recibido observaciones sobre tu proyecto que requieren tu atención.</p>
                            </div>
                            <div class="alert alert-warning" role="alert">
                                <i class="fas fa-exclamation-triangle me-2"></i>
                                <strong>Acción requerida:</strong> Revisa las observaciones antes de continuar con el proceso.
                            </div>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">
                                <i class="fas fa-times me-2"></i>Cancelar
                            </button>
                            <button type="button" class="btn btn-warning" id="confirmObservationsReviewed">
                                <i class="fas fa-check me-2"></i>Ya revisé las observaciones
                            </button>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Modal para resolver observaciones -->
            <div class="modal fade" id="resolveObservationsModal" tabindex="-1" aria-labelledby="resolveObservationsModalLabel" aria-hidden="true">
                <div class="modal-dialog modal-dialog-centered">
                    <div class="modal-content">
                        <div class="modal-header bg-success text-white">
                            <h5 class="modal-title" id="resolveObservationsModalLabel">
                                <i class="fas fa-check-circle me-2"></i>
                                Resolver Observaciones
                            </h5>
                            <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body">
                            <div class="text-center mb-3">
                                <i class="fas fa-tasks fa-3x text-success mb-3"></i>
                                <h5>¿Has resuelto todas las observaciones?</h5>
                                <p class="text-muted">Confirma que has atendido y resuelto todas las observaciones recibidas.</p>
                            </div>
                            <div class="alert alert-success" role="alert">
                                <i class="fas fa-info-circle me-2"></i>
                                <strong>Finalización:</strong> Al confirmar, el proceso se completará exitosamente.
                            </div>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">
                                <i class="fas fa-times me-2"></i>Cancelar
                            </button>
                            <button type="button" class="btn btn-success" id="confirmObservationsResolved">
                                <i class="fas fa-check-double me-2"></i>Sí, resolví las observaciones
                            </button>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Modal de confirmación de decisión de avances -->
            <div class="modal fade" id="progressDecisionModal" tabindex="-1" aria-labelledby="progressDecisionModalLabel" aria-hidden="true">
                <div class="modal-dialog modal-dialog-centered">
                    <div class="modal-content">
                        <div class="modal-header bg-info text-white">
                            <h5 class="modal-title" id="progressDecisionModalLabel">
                                <i class="fas fa-balance-scale me-2"></i>
                                Evaluación de Avances
                            </h5>
                            <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body">
                            <div class="text-center mb-3">
                                <i class="fas fa-question-circle fa-3x text-info mb-3"></i>
                                <h5>¿Los avances recibidos son satisfactorios?</h5>
                                <p class="text-muted">Evalúa si los avances del proyecto cumplen con las expectativas y requerimientos.</p>
                            </div>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-danger" id="rejectProgress">
                                <i class="fas fa-times me-2"></i>No, rechazar avances
                            </button>
                            <button type="button" class="btn btn-success" id="acceptProgress">
                                <i class="fas fa-check me-2"></i>Sí, aceptar avances
                            </button>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Toast container para notificaciones -->
            <div class="toast-container position-fixed top-0 end-0 p-3" id="bmpToastContainer"></div>
        `;
        
        document.body.appendChild(modalContainer);
    }

    // Adjuntar event listeners
    attachEventListeners() {
        // Confirmar envío de avances
        document.getElementById('confirmSendProgress')?.addEventListener('click', () => {
            this.confirmSendProgress();
        });

        // Aceptar avances
        document.getElementById('acceptProgress')?.addEventListener('click', () => {
            this.evaluateProgress(true);
        });

        // Rechazar avances
        document.getElementById('rejectProgress')?.addEventListener('click', () => {
            this.evaluateProgress(false);
        });

        // Confirmar revisión de observaciones
        document.getElementById('confirmObservationsReviewed')?.addEventListener('click', () => {
            this.confirmObservationsReviewed();
        });

        // Confirmar resolución de observaciones
        document.getElementById('confirmObservationsResolved')?.addEventListener('click', () => {
            this.confirmObservationsResolved();
        });
    }

    // Métodos públicos para mostrar popups
    showSendProgressPopup(projectId) {
        this.currentProjectId = projectId;
        const modal = new bootstrap.Modal(document.getElementById('sendProgressModal'));
        modal.show();
    }

    showObservationsAvailablePopup(projectId) {
        this.currentProjectId = projectId;
        const modal = new bootstrap.Modal(document.getElementById('observationsAvailableModal'));
        modal.show();
    }

    showResolveObservationsPopup(projectId) {
        this.currentProjectId = projectId;
        const modal = new bootstrap.Modal(document.getElementById('resolveObservationsModal'));
        modal.show();
    }

    showProgressDecisionPopup(projectId) {
        this.currentProjectId = projectId;
        const modal = new bootstrap.Modal(document.getElementById('progressDecisionModal'));
        modal.show();
    }

    // Métodos privados para manejar las acciones
    async confirmSendProgress() {
        try {
            const response = await fetch(`/api/BmpFlow/confirm-send-progress/${this.currentProjectId}`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({})
            });

            const result = await response.json();
            
            if (response.ok) {
                this.showToast(result.message, 'success');
                bootstrap.Modal.getInstance(document.getElementById('sendProgressModal')).hide();
                
                // Automáticamente continuar al siguiente paso
                setTimeout(() => {
                    this.showProgressDecisionPopup(this.currentProjectId);
                }, 2000);
            } else {
                this.showToast(result.message, 'error');
            }
        } catch (error) {
            this.showToast('Error de conexión: ' + error.message, 'error');
        }
    }

    async evaluateProgress(accepted) {
        try {
            const response = await fetch(`/api/BmpFlow/evaluate-progress-gate/${this.currentProjectId}`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ progressAccepted: accepted })
            });

            const result = await response.json();
            
            if (response.ok) {
                this.showToast(result.message, accepted ? 'success' : 'info');
                bootstrap.Modal.getInstance(document.getElementById('progressDecisionModal')).hide();
                
                if (result.action === 'show_observations_popup') {
                    setTimeout(() => {
                        this.showObservationsAvailablePopup(this.currentProjectId);
                    }, 2000);
                }
            } else {
                this.showToast(result.message, 'error');
            }
        } catch (error) {
            this.showToast('Error de conexión: ' + error.message, 'error');
        }
    }

    async confirmObservationsReviewed() {
        try {
            const response = await fetch(`/api/BmpFlow/confirm-observations-reviewed/${this.currentProjectId}`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' }
            });

            const result = await response.json();
            
            if (response.ok) {
                this.showToast(result.message, 'success');
                bootstrap.Modal.getInstance(document.getElementById('observationsAvailableModal')).hide();
                
                if (result.action === 'resolve_observations') {
                    setTimeout(() => {
                        this.showResolveObservationsPopup(this.currentProjectId);
                    }, 2000);
                }
            } else {
                this.showToast(result.message, 'error');
            }
        } catch (error) {
            this.showToast('Error de conexión: ' + error.message, 'error');
        }
    }

    async confirmObservationsResolved() {
        try {
            const response = await fetch(`/api/BmpFlow/resolve-observations-complete/${this.currentProjectId}`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' }
            });

            const result = await response.json();
            
            if (response.ok) {
                this.showToast(result.message, 'success');
                bootstrap.Modal.getInstance(document.getElementById('resolveObservationsModal')).hide();
                
                if (result.action === 'process_completed') {
                    this.showToast('🎉 ¡Proceso BPM completado exitosamente! El proyecto ha finalizado.', 'success');
                }
            } else {
                this.showToast(result.message, 'error');
            }
        } catch (error) {
            this.showToast('Error de conexión: ' + error.message, 'error');
        }
    }

    // Método para mostrar toasts/notificaciones
    showToast(message, type = 'info') {
        const toastContainer = document.getElementById('bmpToastContainer');
        const toastId = 'toast-' + Date.now();
        
        const bgClass = type === 'error' ? 'bg-danger' : type === 'success' ? 'bg-success' : 'bg-info';
        const iconClass = type === 'error' ? 'fa-exclamation-circle' : type === 'success' ? 'fa-check-circle' : 'fa-info-circle';
        
        const toastHTML = `
            <div id="${toastId}" class="toast ${bgClass} text-white" role="alert" aria-live="assertive" aria-atomic="true">
                <div class="d-flex">
                    <div class="toast-body">
                        <i class="fas ${iconClass} me-2"></i>
                        ${message}
                    </div>
                    <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
                </div>
            </div>
        `;
        
        toastContainer.insertAdjacentHTML('beforeend', toastHTML);
        
        const toastElement = document.getElementById(toastId);
        const toast = new bootstrap.Toast(toastElement, { delay: 5000 });
        toast.show();
        
        // Limpiar el toast después de que se oculte
        toastElement.addEventListener('hidden.bs.toast', () => {
            toastElement.remove();
        });
    }
}

// Inicializar el servicio globalmente
window.bmpFlowService = new BmpFlowPopupService();